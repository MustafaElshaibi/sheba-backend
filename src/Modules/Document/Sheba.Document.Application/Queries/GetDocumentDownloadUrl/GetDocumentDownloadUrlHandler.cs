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

        var url = await storage.GetPresignedDownloadUrlAsync(doc.BucketName, doc.ObjectKey, UrlExpiry, ct);

        return new DocumentDownloadUrlDto(
            doc.Id, doc.FileName, doc.ContentType, url, DateTime.UtcNow.Add(UrlExpiry));
    }
}
