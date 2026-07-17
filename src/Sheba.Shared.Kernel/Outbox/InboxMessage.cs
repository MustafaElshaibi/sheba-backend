using Sheba.Shared.Kernel.Entities;

namespace Sheba.Shared.Kernel.Outbox;

/// <summary>
/// Consumer-side idempotency record (T-EVT-1). The outbox dispatcher delivers at-least-once —
/// a retried event republishes to every handler, not just the ones that previously failed — so
/// each consumer records (EventId, ConsumerName) here before/after doing its work and skips
/// events it has already processed. One row per (event, consumer) pair, never per (event, module):
/// two handlers in the same module consuming the same event are independent consumers.
/// </summary>
public sealed class InboxMessage : BaseEntity
{
    public Guid EventId { get; private set; }
    public string ConsumerName { get; private set; } = string.Empty;
    public DateTime ProcessedAt { get; private set; }

    private InboxMessage() { }

    public static InboxMessage Create(Guid eventId, string consumerName) => new()
    {
        EventId = eventId,
        ConsumerName = consumerName,
        ProcessedAt = DateTime.UtcNow
    };
}
