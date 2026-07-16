using Sheba.Shared.Kernel.Entities;

namespace Sheba.Document.Domain.Entities;

/// <summary>
/// Grants a non-owner (e.g. a ministry reviewer) time-boxed read access to a document.
/// Placeholder for full access-control implementation.
/// </summary>
public sealed class DocumentAccessGrant : BaseEntity
{
    public Guid DocumentId { get; private set; }
    public Guid GranteeId { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private DocumentAccessGrant() { }

    public static DocumentAccessGrant Create(Guid documentId, Guid granteeId, DateTime expiresAt)
        => new() { DocumentId = documentId, GranteeId = granteeId, ExpiresAt = expiresAt };
}
