using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Document.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;
using DocEntity = Sheba.Document.Domain.Entities.Document;

namespace Sheba.Document.Application.Commands.UploadDocument;

public sealed class UploadDocumentHandler(
    IDocumentRepository repository,
    IFileStorage storage,
    ILogger<UploadDocumentHandler> logger
) : IRequestHandler<UploadDocumentCommand, UploadDocumentResponse>
{
    private const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const string BucketName = "sheba-documents";

    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg", "image/png", "image/webp", "application/pdf"
    ];

    public async Task<UploadDocumentResponse> Handle(UploadDocumentCommand command, CancellationToken ct)
    {
        var file = command.File;

        // ── Validation ────────────────────────────────────────────────────────
        if (file is null || file.Length == 0)
            throw new DomainException("No file provided or file is empty.");

        if (file.Length > MaxSizeBytes)
            throw new DomainException($"File exceeds the maximum size of {MaxSizeBytes / (1024 * 1024)} MB.");

        var contentType = file.ContentType?.ToLowerInvariant() ?? "";
        if (!AllowedContentTypes.Contains(contentType))
            throw new DomainException($"Unsupported file type '{file.ContentType}'. Allowed: JPEG, PNG, WebP, PDF.");

        // ── Object key: {ownerId}/{guid}{ext} ─────────────────────────────────
        var ext = Path.GetExtension(file.FileName);
        var objectKey = $"{command.OwnerId}/{Guid.NewGuid():N}{ext}";

        // ── Upload to MinIO ───────────────────────────────────────────────────
        await using (var stream = file.OpenReadStream())
        {
            await storage.UploadAsync(BucketName, objectKey, stream, file.Length, contentType, ct);
        }

        // ── Persist metadata ──────────────────────────────────────────────────
        var document = DocEntity.Create(
            command.OwnerId, file.FileName, contentType, file.Length,
            BucketName, objectKey, command.DocumentType);

        await repository.AddAsync(document, ct);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[UploadDocument] Stored {Id} for owner {Owner}", document.Id, command.OwnerId);

        return new UploadDocumentResponse(document.Id, document.FileName, document.SizeBytes,
            "Document uploaded successfully.");
    }
}
