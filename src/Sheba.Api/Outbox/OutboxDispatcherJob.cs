using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sheba.Shared.Kernel.Outbox;

namespace Sheba.Api.Outbox;

/// <summary>
/// Polls every module's outbox_messages table and publishes pending/retryable rows via MediatR
/// (T-EVT-1). Registered as a single Hangfire recurring job ("outbox-dispatcher", every minute —
/// Hangfire's native cron scheduler has no sub-minute granularity) that internally re-polls every
/// 5 seconds for the rest of its minute window, matching the "5 s poll" target in TASKS.md without
/// the risk of self-rescheduling chains accumulating across app restarts (Hangfire recurring jobs
/// are deduplicated by id, so restarts never create a second concurrent dispatcher).
///
/// No PII is ever logged here — only event type names and outbox row ids, never the payload.
/// </summary>
public sealed class OutboxDispatcherJob(
    IServiceProvider serviceProvider,
    ILogger<OutboxDispatcherJob> logger)
{
    private const int MaxAttempts = 8;
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan JobWindow = TimeSpan.FromSeconds(55);

    public async Task DispatchAsync(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.Add(JobWindow);
        do
        {
            await DispatchOnceAsync(ct);
            await Task.Delay(PollInterval, ct);
        } while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested);
    }

    private async Task DispatchOnceAsync(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var contexts = scope.ServiceProvider.GetServices<DbContext>();
        var now = DateTime.UtcNow;

        foreach (var context in contexts)
        {
            var pending = await context.Set<OutboxMessage>()
                .Where(m => m.Status == OutboxMessageStatus.Pending || m.Status == OutboxMessageStatus.Failed)
                .Where(m => m.NextAttemptAt <= now)
                .OrderBy(m => m.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (pending.Count == 0)
                continue;

            foreach (var message in pending)
                await PublishOneAsync(publisher, message, ct);

            await context.SaveChangesAsync(ct);
        }
    }

    private async Task PublishOneAsync(IPublisher publisher, OutboxMessage message, CancellationToken ct)
    {
        try
        {
            var eventType = Type.GetType(message.EventType)
                ?? throw new InvalidOperationException($"Cannot resolve outbox event type '{message.EventType}'.");

            if (JsonSerializer.Deserialize(message.Payload, eventType) is not INotification notification)
                throw new InvalidOperationException($"Outbox event type '{message.EventType}' is not an INotification.");

            await publisher.Publish(notification, ct);
            message.MarkPublished();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[Outbox] Failed to publish {EventType} (outbox row {MessageId}), attempt {Attempt}",
                message.EventType, message.Id, message.Attempts + 1);
            message.MarkFailed(ex.Message, DateTime.UtcNow.AddSeconds(Math.Pow(2, message.Attempts + 1)), MaxAttempts);
        }
    }
}
