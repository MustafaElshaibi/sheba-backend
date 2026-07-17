using MediatR;

namespace Sheba.Document.Application.Queries.GetDocumentDownloadUrl;

/// <summary>Ownership is enforced in the handler — a citizen only gets a URL for their own document.</summary>
public sealed record GetDocumentDownloadUrlQuery(Guid DocumentId, Guid ActorId, bool IsAdmin)
    : IRequest<DocumentDownloadUrlDto?>;

public sealed record DocumentDownloadUrlDto(
    Guid DocumentId,
    string FileName,
    string ContentType,
    string DownloadUrl,
    DateTime ExpiresAt);
