using Sheba.Shared.Kernel.Events;

namespace Sheba.Identity.Domain.DomainEvents;

/// <summary>
/// Raised when an admin approves or rejects an identity request.
/// Handlers:
///   - Identity Application: SendApprovalEmailHandler / SendRejectionEmailHandler
///   - Wallet module (on approve): issue Verifiable Credential
///   - Audit module: records the decision
/// </summary>
public sealed record IdentityRequestDecidedEvent(
    Guid RequestId,
    Guid AccountId,
    bool Approved,
    string? RejectionReason
) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
