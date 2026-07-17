namespace Sheba.Shared.Kernel.Events.IntegrationEvents;

/// <summary>
/// Raised when an admin approves or rejects an identity request. Declared in Shared.Kernel (not
/// Identity.Domain) because Admin and Wallet subscribe to it — a cross-module event contract must
/// live where every consumer can reference it without pulling in the producer's Domain assembly
/// (rule 1/3, T-ARC-1).
///
/// Handlers:
///   - Identity Application: SendApprovalEmailHandler / SendRejectionEmailHandler
///   - Wallet module (on approve): issue Verifiable Credential
///   - Admin module: updates daily registration analytics snapshot
/// </summary>
public sealed record IdentityRequestDecidedEvent(
    Guid RequestId,
    Guid AccountId,
    bool Approved,
    string? RejectionReason
) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
