namespace Sheba.Shared.Kernel.Events.IntegrationEvents;

/// <summary>
/// Raised when an account is closed (admin closure or citizen-requested), a terminal transition
/// per sheba.md §6.2. Declared in Shared.Kernel because Wallet subscribes to revoke the account's
/// VCs (BR-WA-1) alongside Identity's own citizen-notification handler.
/// </summary>
public sealed record AccountDeactivatedEvent(
    Guid AccountId,
    string? Reason
) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
