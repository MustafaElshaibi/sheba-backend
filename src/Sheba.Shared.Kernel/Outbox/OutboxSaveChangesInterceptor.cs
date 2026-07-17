using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Events;

namespace Sheba.Shared.Kernel.Outbox;

/// <summary>
/// EF Core SaveChanges interceptor (T-EVT-1): before every commit, converts each tracked
/// aggregate's raised domain events into <see cref="OutboxMessage"/> rows and adds them to the
/// same DbContext, so they persist in the same transaction as the aggregate write. This replaces
/// the old per-repository pattern of publishing events in-process at SaveChanges (in-process
/// publish had no durability — a crash after publish but before commit could fire a handler for
/// state that never landed; a crash after commit but before publish silently dropped the event).
/// Registered once per module via <c>optionsBuilder.AddInterceptors(new OutboxSaveChangesInterceptor())</c> —
/// it is stateless, so a single instance is safe to share across the module's DbContext pool.
/// Actual publishing happens later, out-of-band, by the Hangfire outbox dispatcher in Sheba.Api.
/// </summary>
public sealed class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AppendOutboxMessages(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AppendOutboxMessages(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void AppendOutboxMessages(DbContext? context)
    {
        if (context is null)
            return;

        var entitiesWithEvents = context.ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                if (domainEvent is not IDomainEvent evt)
                    continue;

                var eventType = evt.GetType();
                var payload = JsonSerializer.Serialize(evt, eventType);

                context.Set<OutboxMessage>().Add(OutboxMessage.Create(
                    eventId: evt.EventId,
                    aggregateType: entity.GetType().Name,
                    aggregateId: entity.Id,
                    eventType: eventType.AssemblyQualifiedName!,
                    payload: payload));
            }

            entity.ClearDomainEvents();
        }
    }
}
