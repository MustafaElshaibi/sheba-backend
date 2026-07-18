namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// Cross-module read-only port over the Document module. Defined in Shared.Kernel so
/// ServiceRequest (which gates submission on required documents, BR-SR-2/T-SRV-3) never
/// references Sheba.Document.Domain/Infrastructure directly (rule 1/3, T-ARC-1). Implemented in
/// Document.Infrastructure, which owns the `document.documents` table.
/// </summary>
public interface IDocumentPort
{
    /// <summary>The distinct, non-deleted document types the owner currently has on file.</summary>
    Task<IReadOnlySet<string>> GetOwnerDocumentTypesAsync(Guid ownerId, CancellationToken ct = default);
}
