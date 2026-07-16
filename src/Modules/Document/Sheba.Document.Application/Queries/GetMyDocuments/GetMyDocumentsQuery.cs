using MediatR;

namespace Sheba.Document.Application.Queries.GetMyDocuments;

public sealed record GetMyDocumentsQuery(Guid OwnerId) : IRequest<List<DocumentSummaryDto>>;

public sealed record DocumentSummaryDto(
    Guid Id, string FileName, string ContentType,
    long SizeBytes, string DocumentType, DateTime CreatedAt);
