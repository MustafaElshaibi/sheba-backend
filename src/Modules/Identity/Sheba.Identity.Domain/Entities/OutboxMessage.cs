using Sheba.Shared.Kernel.Entities;

namespace Sheba.Identity.Domain.Entities;

public sealed class OutboxMessage : BaseEntity
{
    public string AggregateType { get; private set; } = string.Empty;
    public Guid AggregateId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = "{}";
    public DateTime? PublishedAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(
        string aggregateType,
        Guid aggregateId,
        string eventType,
        string payload)
    {
        return new OutboxMessage
        {
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            EventType = eventType,
            Payload = payload
        };
    }

    public void MarkPublished()
    {
        PublishedAt = DateTime.UtcNow;
        Touch();
    }

    public void MarkFailed(string error)
    {
        Error = error;
        RetryCount++;
        Touch();
    }
}