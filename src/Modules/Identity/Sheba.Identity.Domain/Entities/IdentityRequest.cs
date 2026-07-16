using Sheba.Identity.Domain.DomainEvents;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;
using System.Text.Json;

namespace Sheba.Identity.Domain.Entities;

/// <summary>
/// An eKYC / admin-review request submitted by a citizen.
/// Represents a single workflow instance: OPEN_ACCOUNT, UPGRADE_LOA2, etc.
/// </summary>
public sealed class IdentityRequest : BaseEntity
{
    public Guid AccountId { get; private set; }
    public RequestType RequestType { get; private set; }
    public RequestStatus Status { get; private set; } = RequestStatus.Pending;

    public DateTime SubmittedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedByAdminId { get; private set; }

    public string? RejectionReason { get; private set; }
    public string? AdminNotes { get; private set; }

    /// <summary>
    /// Snapshot of civil registry data captured at submission time.
    /// Stored as JSON so it is immutable and auditable even if registry data changes.
    /// </summary>
    public string CitizenSnapshotJson { get; private set; } = "{}";

    /// <summary>Deserializes the snapshot to a dynamic object for display purposes.</summary>
    public object CitizenSnapshot
    {
        get
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                    CitizenSnapshotJson);
            }
            catch { return new { }; }
        }
    }

    // EF Core
    private IdentityRequest() { }

    /// <summary>Creates a new identity request. Domain event is raised when MarkUnderReview is called
    /// (i.e. when CompleteRegistration completes), not at creation time.</summary>
    public static IdentityRequest Submit(
        Guid accountId,
        RequestType requestType,
        object citizenSnapshot)
    {
        var request = new IdentityRequest
        {
            AccountId          = accountId,
            RequestType        = requestType,
            Status             = RequestStatus.Pending,
            SubmittedAt        = DateTime.UtcNow,
            CitizenSnapshotJson = JsonSerializer.Serialize(citizenSnapshot)
        };

        // Domain event NOT raised here — raised in MarkUnderReview() when the citizen
        // completes registration with credentials (CompleteRegistration step).
        return request;
    }

    /// <summary>Admin opens the request for review. Alias: MarkUnderReview.</summary>
    public void BeginReview(Guid adminId)
    {
        if (Status != RequestStatus.Pending)
            throw new DomainException($"Cannot begin review on a request in status {Status}.");

        Status            = RequestStatus.UnderReview;
        ReviewedByAdminId = adminId;
        Touch();
    }

    /// <summary>Convenience alias used by CompleteRegistration handler — system-initiated, no admin yet.
    /// Raises IdentityRequestSubmittedEvent to notify admin reviewers that registration is complete.</summary>
    public void MarkUnderReview()
    {
        if (Status != RequestStatus.Pending)
            throw new DomainException($"Cannot mark request as under review in status {Status}.");

        Status            = RequestStatus.UnderReview;
        ReviewedByAdminId = Guid.Empty; // system-initiated; no human reviewer yet
        Touch();

        // Raise event here — admin gets notified only after citizen completes registration
        RaiseDomainEvent(
            new IdentityRequestSubmittedEvent(Id, AccountId, RequestType));
    }

    /// <summary>Admin approves the request.</summary>
    public void Approve(Guid adminId, string? notes = null)
    {
        if (Status is not (RequestStatus.Pending or RequestStatus.UnderReview))
            throw new DomainException($"Cannot approve a request in status {Status}.");

        Status            = RequestStatus.Approved;
        ReviewedAt        = DateTime.UtcNow;
        ReviewedByAdminId = adminId;
        AdminNotes        = notes;
        Touch();

        RaiseDomainEvent(
            new IdentityRequestDecidedEvent(Id, AccountId, Approved: true, RejectionReason: null));
    }

    /// <summary>Admin rejects the request with a reason.</summary>
    public void Reject(Guid adminId, string rejectionReason, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new DomainException("Rejection reason is required.");

        if (Status is not (RequestStatus.Pending or RequestStatus.UnderReview))
            throw new DomainException($"Cannot reject a request in status {Status}.");

        Status            = RequestStatus.Rejected;
        ReviewedAt        = DateTime.UtcNow;
        ReviewedByAdminId = adminId;
        RejectionReason   = rejectionReason;
        AdminNotes        = notes;
        Touch();

        RaiseDomainEvent(
            new IdentityRequestDecidedEvent(Id, AccountId, Approved: false, RejectionReason: rejectionReason));
    }

    /// <summary>Citizen cancels the request before a decision is made.</summary>
    public void Cancel()
    {
        if (Status is RequestStatus.Approved or RequestStatus.Rejected)
            throw new DomainException("Cannot cancel an already decided request.");

        Status = RequestStatus.Cancelled;
        Touch();
    }
}
