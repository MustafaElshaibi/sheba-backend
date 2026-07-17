using Sheba.Shared.Kernel.Entities;

namespace Sheba.Shared.Kernel.Outbox;

public enum OutboxMessageStatus
{
    Pending = 0,
    Published = 1,
    Failed = 2,
    DeadLettered = 3
}

/// <summary>
/// One row per domain event raised in a module's transaction. Written by
/// <see cref="OutboxSaveChangesInterceptor"/> in the same SaveChanges call as the aggregate
/// write, so the event and the state change commit atomically (T-EVT-1). A Hangfire dispatcher
/// polls <see cref="Status"/> == Pending/Failed rows across every module schema, publishes them
/// via MediatR, and marks the outcome. Rows are kept (not deleted) after publishing so a future
/// BI-rebuild tool (T-ADM-1) can replay history from the outbox.
/// </summary>
public sealed class OutboxMessage : BaseEntity
{
    public Guid EventId { get; private set; }
    public string AggregateType { get; private set; } = string.Empty;
    public Guid AggregateId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = "{}";
    public OutboxMessageStatus Status { get; private set; } = OutboxMessageStatus.Pending;
    public int Attempts { get; private set; }
    public DateTime NextAttemptAt { get; private set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(
        Guid eventId, string aggregateType, Guid aggregateId, string eventType, string payload)
    {
        return new OutboxMessage
        {
            EventId = eventId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            EventType = eventType,
            Payload = payload
        };
    }

    public void MarkPublished()
    {
        Status = OutboxMessageStatus.Published;
        PublishedAt = DateTime.UtcNow;
        LastError = null;
        Touch();
    }

    public void MarkFailed(string error, DateTime nextAttemptAt, int maxAttempts)
    {
        Attempts++;
        LastError = error;
        Status = Attempts >= maxAttempts ? OutboxMessageStatus.DeadLettered : OutboxMessageStatus.Failed;
        NextAttemptAt = nextAttemptAt;
        Touch();
    }
}
