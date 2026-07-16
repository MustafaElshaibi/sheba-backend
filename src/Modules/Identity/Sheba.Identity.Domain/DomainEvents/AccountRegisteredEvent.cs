using Sheba.Shared.Kernel.Events;

namespace Sheba.Identity.Domain.DomainEvents;

/// <summary>
/// Raised when a citizen completes Step 1 of registration (NID check passes).
/// Handlers: none at this stage (OTP is sent synchronously by the command handler).
/// </summary>
public sealed record AccountRegisteredEvent(
    Guid AccountId,
    string NationalId
) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
