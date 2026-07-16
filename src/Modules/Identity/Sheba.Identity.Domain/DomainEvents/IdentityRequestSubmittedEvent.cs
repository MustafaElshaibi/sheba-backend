using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Events;

namespace Sheba.Identity.Domain.DomainEvents;

/// <summary>
/// Raised when a citizen submits a complete identity request (after OTP + email verified).
/// Handlers:
///   - Notification module: sends admin notification email to all IDENTITY_REVIEWER admins
///   - Audit module: records the submission event
/// </summary>
public sealed record IdentityRequestSubmittedEvent(
    Guid RequestId,
    Guid AccountId,
    RequestType RequestType
) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
