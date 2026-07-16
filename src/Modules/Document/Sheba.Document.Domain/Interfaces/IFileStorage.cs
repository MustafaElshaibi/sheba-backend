namespace Sheba.Document.Domain.Interfaces;

/// <summary>
/// Port for object storage (MinIO / S3). Implemented in Infrastructure.
/// The Application layer depends on this abstraction, never on the MinIO SDK directly.
/// </summary>
public interface IFileStorage
{
    /// <summary>Ensures the bucket exists, then uploads the stream. Returns the object key.</summary>
    Task<string> UploadAsync(
        string bucketName,
        string objectKey,
        Stream data,
        long size,
        string contentType,
        CancellationToken ct = default);

    /// <summary>Generates a presigned GET URL valid for the given duration.</summary>
    Task<string> GetPresignedDownloadUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan expiry,
        CancellationToken ct = default);

    /// <summary>Deletes an object from the bucket.</summary>
    Task DeleteAsync(string bucketName, string objectKey, CancellationToken ct = default);
}
