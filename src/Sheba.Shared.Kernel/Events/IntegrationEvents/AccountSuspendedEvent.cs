namespace Sheba.Shared.Kernel.Events.IntegrationEvents;

/// <summary>
/// Raised when an admin places a security hold on an Approved account (BR-LG §6.2). Declared in
/// Shared.Kernel because Wallet subscribes to revoke the account's VCs (BR-WA-1) alongside
/// Identity's own citizen-notification handler.
/// </summary>
public sealed record AccountSuspendedEvent(
    Guid AccountId,
    string? Reason
) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
