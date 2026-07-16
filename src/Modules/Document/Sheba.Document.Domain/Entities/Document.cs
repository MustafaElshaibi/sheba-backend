using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Document.Domain.Entities;

/// <summary>
/// Metadata for a file stored in MinIO object storage.
/// The actual bytes live in MinIO; this row tracks ownership, type, and the object key.
/// </summary>
public sealed class Document : BaseEntity
{
    public Guid OwnerId { get; private set; }                 // citizen/account that owns the file
    public string FileName { get; private set; } = string.Empty;     // original client file name
    public string ContentType { get; private set; } = string.Empty;  // MIME type
    public long SizeBytes { get; private set; }
    public string BucketName { get; private set; } = string.Empty;
    public string ObjectKey { get; private set; } = string.Empty;    // key inside the bucket
    public string DocumentType { get; private set; } = "GENERAL";    // NATIONAL_ID_PHOTO, SELFIE, etc.
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private Document() { }

    public static Document Create(
        Guid ownerId,
        string fileName,
        string contentType,
        long sizeBytes,
        string bucketName,
        string objectKey,
        string documentType = "GENERAL")
    {
        if (ownerId == Guid.Empty) throw new DomainException("Owner ID is required.");
        if (string.IsNullOrWhiteSpace(fileName)) throw new DomainException("File name is required.");
        if (sizeBytes <= 0) throw new DomainException("File is empty.");

        return new Document
        {
            OwnerId = ownerId,
            FileName = fileName.Trim(),
            ContentType = contentType,
            SizeBytes = sizeBytes,
            BucketName = bucketName,
            ObjectKey = objectKey,
            DocumentType = documentType.Trim().ToUpperInvariant()
        };
    }

    public void MarkDeleted()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        Touch();
    }
}
