using Sheba.Document.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Document.Infrastructure.Adapters;

/// <summary>Implements <see cref="IDocumentPort"/> for cross-module required-document checks (T-SRV-3).</summary>
public sealed class DocumentPortAdapter(IDocumentRepository repository) : IDocumentPort
{
    public async Task<IReadOnlySet<string>> GetOwnerDocumentTypesAsync(Guid ownerId, CancellationToken ct = default)
    {
        var documents = await repository.GetByOwnerAsync(ownerId, ct);
        return documents.Select(d => d.DocumentType).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
