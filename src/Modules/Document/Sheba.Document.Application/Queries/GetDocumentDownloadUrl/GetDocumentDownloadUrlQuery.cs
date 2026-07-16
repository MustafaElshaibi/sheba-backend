using MediatR;

namespace Sheba.Document.Application.Queries.GetDocumentDownloadUrl;

public sealed record GetDocumentDownloadUrlQuery(Guid DocumentId)
    : IRequest<DocumentDownloadUrlDto?>;

public sealed record DocumentDownloadUrlDto(
    Guid DocumentId,
    string FileName,
    string ContentType,
    string DownloadUrl,
    DateTime ExpiresAt);
