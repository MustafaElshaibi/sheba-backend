using MediatR;
using Sheba.Document.Domain.Interfaces;

namespace Sheba.Document.Application.Queries.GetMyDocuments;

public sealed class GetMyDocumentsHandler(IDocumentRepository repository)
    : IRequestHandler<GetMyDocumentsQuery, List<DocumentSummaryDto>>
{
    public async Task<List<DocumentSummaryDto>> Handle(GetMyDocumentsQuery query, CancellationToken ct)
    {
        var docs = await repository.GetByOwnerAsync(query.OwnerId, ct);
        return docs.Select(d => new DocumentSummaryDto(
            d.Id, d.FileName, d.ContentType, d.SizeBytes, d.DocumentType, d.CreatedAt
        )).ToList();
    }
}
