using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Sheba.Document.Domain.Interfaces;

namespace Sheba.Document.Infrastructure.Storage;

/// <summary>
/// MinIO (S3-compatible) implementation of IFileStorage.
/// Configuration keys:
///   Minio:Endpoint   (e.g. "localhost:9000" or "minio:9000")
///   Minio:AccessKey
///   Minio:SecretKey
///   Minio:UseSsl     (bool, default false)
/// </summary>
public sealed class MinioFileStorage : IFileStorage
{
    private readonly IMinioClient _client;
    private readonly ILogger<MinioFileStorage> _logger;
    private readonly string _publicEndpoint;

    public MinioFileStorage(IConfiguration configuration, ILogger<MinioFileStorage> logger)
    {
        _logger = logger;

        var endpoint = configuration["Minio:Endpoint"] ?? "localhost:9000";
        var accessKey = configuration["Minio:AccessKey"] ?? "minioadmin";
        var secretKey = configuration["Minio:SecretKey"] ?? "minioadmin";
        var useSsl = bool.TryParse(configuration["Minio:UseSsl"], out var ssl) && ssl;
        _publicEndpoint = configuration["Minio:PublicEndpoint"] ?? endpoint;

        _client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();
    }

    public async Task<string> UploadAsync(
        string bucketName, string objectKey, Stream data, long size, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketAsync(bucketName, ct);

        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithStreamData(data)
            .WithObjectSize(size)
            .WithContentType(contentType), ct);

        _logger.LogInformation("[MinIO] Uploaded {Key} ({Size} bytes) to {Bucket}", objectKey, size, bucketName);
        return objectKey;
    }

    public async Task<string> GetPresignedDownloadUrlAsync(
        string bucketName, string objectKey, TimeSpan expiry, CancellationToken ct = default)
    {
        var url = await _client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithExpiry((int)expiry.TotalSeconds));

        _logger.LogInformation("[MinIO] Generated presigned URL for {Key} (expires in {Min} min)",
            objectKey, expiry.TotalMinutes);
        return url;
    }

    public async Task DeleteAsync(string bucketName, string objectKey, CancellationToken ct = default)
    {
        await _client.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey), ct);
        _logger.LogInformation("[MinIO] Deleted {Key} from {Bucket}", objectKey, bucketName);
    }

    private async Task EnsureBucketAsync(string bucketName, CancellationToken ct)
    {
        var exists = await _client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucketName), ct);
        if (!exists)
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName), ct);
            _logger.LogInformation("[MinIO] Created bucket {Bucket}", bucketName);
        }
    }
}
