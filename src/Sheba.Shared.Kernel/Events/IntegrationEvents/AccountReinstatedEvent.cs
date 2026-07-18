namespace Sheba.Shared.Kernel.Events.IntegrationEvents;

/// <summary>
/// Raised when an admin lifts a security hold on a Suspended account (BR-LG §6.2). Handlers:
/// Identity Application's citizen-notification email. Wallet does not re-issue VCs on
/// reinstatement — a revoked credential stays revoked; the citizen's LoA-gated re-issuance flow
/// (if any) is out of this event's scope.
/// </summary>
public sealed record AccountReinstatedEvent(
    Guid AccountId
) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
