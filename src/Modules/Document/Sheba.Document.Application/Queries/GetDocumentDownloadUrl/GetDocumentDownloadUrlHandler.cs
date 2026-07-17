using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Document.Domain.Interfaces;

namespace Sheba.Document.Application.Queries.GetDocumentDownloadUrl;

public sealed class GetDocumentDownloadUrlHandler(
    IDocumentRepository repository,
    IFileStorage storage,
    ILogger<GetDocumentDownloadUrlHandler> logger
) : IRequestHandler<GetDocumentDownloadUrlQuery, DocumentDownloadUrlDto?>
{
    private static readonly TimeSpan UrlExpiry = TimeSpan.FromMinutes(15);

    public async Task<DocumentDownloadUrlDto?> Handle(GetDocumentDownloadUrlQuery query, CancellationToken ct)
    {
        var doc = await repository.GetByIdAsync(query.DocumentId, ct);
        if (doc is null)
        {
            logger.LogWarning("[GetDocumentDownloadUrl] Document {Id} not found", query.DocumentId);
            return null;
        }

        // BR-DO-1: owners see only their own documents (a time-boxed access grant for non-owners
        // is designed but not yet implemented — until then, non-admin non-owners get null, same
        // as "not found", so this never confirms another citizen's document exists).
        if (!query.IsAdmin && doc.OwnerId != query.ActorId)
        {
            logger.LogWarning(
                "[GetDocumentDownloadUrl] Actor {ActorId} is not the owner of Document {Id}",
                query.ActorId, query.DocumentId);
            return null;
        }

        var url = await storage.GetPresignedDownloadUrlAsync(doc.BucketName, doc.ObjectKey, UrlExpiry, ct);

        return new DocumentDownloadUrlDto(
            doc.Id, doc.FileName, doc.ContentType, url, DateTime.UtcNow.Add(UrlExpiry));
    }
}
