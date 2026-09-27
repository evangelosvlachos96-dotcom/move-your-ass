using System.Globalization;
using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Mya.Application.Abstractions.Media;
using Mya.Application.Abstractions.System;

namespace Mya.Infrastructure.Storage;

/// <summary>
/// S3-compatible object storage (ADR-019). The browser PUTs parts straight to the provider with
/// presigned URLs; no video bytes pass through the API. The multipart lifecycle is server-side —
/// create, complete and abort are signed API calls, and completion reads the part ETags with
/// ListParts rather than trusting the browser, so the bucket never has to expose ETag over CORS.
/// </summary>
public sealed class S3VideoStorage : IVideoStorage, IDisposable
{
    private static readonly string[] ContentTypes = ["video/mp4", "video/quicktime"];

    private readonly IOptions<S3VideoSettings> _options;
    private readonly IClock _clock;
    private readonly Lazy<IAmazonS3> _client;

    public S3VideoStorage(IOptions<S3VideoSettings> options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _clock = clock;
        _client = new Lazy<IAmazonS3>(CreateClient);
    }

    /// <summary>Test seam: compose the adapter over a client pointed at a local MinIO container.</summary>
    public S3VideoStorage(IOptions<S3VideoSettings> options, IClock clock, IAmazonS3 client)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _clock = clock;
        _client = new Lazy<IAmazonS3>(() => client);
    }

    private S3VideoSettings Settings => _options.Value;

    private IAmazonS3 Client => IsConfigured
        ? _client.Value
        : throw new InvalidOperationException("Video storage is not configured.");

    public bool IsConfigured => Settings.Enabled;

    public long MaxFileBytes => Settings.MaxFileBytes;

    public long StorageCapBytes => Settings.StorageCapBytes;

    public long PartSizeBytes => Settings.PartSizeBytes;

    public IReadOnlyList<string> AllowedContentTypes => ContentTypes;

    public TimeSpan PlaybackLifetime => TimeSpan.FromMinutes(Settings.PlaybackMinutes);

    public TimeSpan UploadLifetime => TimeSpan.FromMinutes(Settings.UploadMinutes);

    /// <summary>
    /// A presigned URL defaults to HTTPS regardless of the endpoint, which is right for B2 and
    /// wrong for a plain-HTTP MinIO container in a test. Follow the configured endpoint instead.
    /// </summary>
    private Protocol Scheme => Client.Config.ServiceURL?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true
        ? Protocol.HTTP
        : Protocol.HTTPS;

    public async Task<UploadSession> BeginUploadAsync(string objectKey, string contentType, long sizeBytes, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);

        var parts = VideoStorageMath.PartCount(sizeBytes, Settings.PartSizeBytes);
        var response = await CallAsync(() => Client.InitiateMultipartUploadAsync(
            new InitiateMultipartUploadRequest
            {
                BucketName = Settings.BucketName,
                Key = objectKey,
                ContentType = contentType,
            },
            ct));

        return new UploadSession(objectKey, response.UploadId, Settings.PartSizeBytes, parts);
    }

    public IReadOnlyList<PartUrl> PresignPartUrls(string objectKey, string uploadId, int fromPart, int toPart)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadId);
        ArgumentOutOfRangeException.ThrowIfLessThan(fromPart, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(toPart, fromPart);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(toPart, S3VideoSettings.MaximumParts);

        var expires = _clock.UtcNow.Add(UploadLifetime);
        var urls = new List<PartUrl>(toPart - fromPart + 1);
        for (var part = fromPart; part <= toPart; part++)
        {
            // Content-Type is deliberately left unsigned. SigV4 query signing then covers only
            // Host, so the browser cannot break a part upload by labelling the blob differently.
            urls.Add(new PartUrl(part, Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = Settings.BucketName,
                Key = objectKey,
                Verb = HttpVerb.PUT,
                Protocol = Scheme,
                UploadId = uploadId,
                PartNumber = part,
                Expires = expires,
            })));
        }

        return urls;
    }

    public async Task<IReadOnlyList<int>> ListUploadedPartsAsync(string objectKey, string uploadId, CancellationToken ct)
    {
        try
        {
            return [.. (await ListPartsAsync(objectKey, uploadId, ct)).Select(p => p.PartNumber!.Value)];
        }
        catch (VideoStorageException e) when (e.NotFound)
        {
            // The provider forgot the upload. The caller starts a fresh one.
            return [];
        }
    }

    public async Task<StoredObject> CompleteUploadAsync(string objectKey, string uploadId, CancellationToken ct)
    {
        var parts = await ListPartsAsync(objectKey, uploadId, ct);

        if (parts.Count == 0)
        {
            throw new VideoStorageException("The upload has no parts.");
        }

        await CallAsync(() => Client.CompleteMultipartUploadAsync(
            new CompleteMultipartUploadRequest
            {
                BucketName = Settings.BucketName,
                Key = objectKey,
                UploadId = uploadId,
                PartETags = parts,
            },
            ct));

        return await HeadAsync(objectKey, ct)
            ?? throw new VideoStorageException("The completed object could not be read back.");
    }

    public async Task AbortUploadAsync(string objectKey, string uploadId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadId);

        try
        {
            await CallAsync(() => Client.AbortMultipartUploadAsync(
                new AbortMultipartUploadRequest
                {
                    BucketName = Settings.BucketName,
                    Key = objectKey,
                    UploadId = uploadId,
                },
                ct));
        }
        catch (VideoStorageException e) when (e.NotFound)
        {
            // Already gone. Nothing is being billed for, which is the point of aborting.
        }
    }

    public async Task<StoredObject?> HeadAsync(string objectKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        try
        {
            var head = await CallAsync(() => Client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest { BucketName = Settings.BucketName, Key = objectKey },
                ct));

            return new StoredObject(head.ContentLength, head.Headers?.ContentType ?? string.Empty);
        }
        catch (VideoStorageException e) when (e.NotFound)
        {
            return null;
        }
    }

    public PlaybackLink PresignGet(string objectKey, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        var expires = _clock.UtcNow.Add(lifetime);
        var url = Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = Settings.BucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Protocol = Scheme,
            Expires = expires,
        });

        return new PlaybackLink(url, new DateTimeOffset(expires, TimeSpan.Zero).ToUnixTimeSeconds());
    }

    public string PresignPut(string objectKey, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        return Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = Settings.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Protocol = Scheme,
            Expires = _clock.UtcNow.Add(lifetime),
        });
    }

    public async Task DeleteAsync(string objectKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        try
        {
            await CallAsync(() => Client.DeleteObjectAsync(
                new DeleteObjectRequest { BucketName = Settings.BucketName, Key = objectKey },
                ct));
        }
        catch (VideoStorageException e) when (e.NotFound)
        {
            // S3 delete is idempotent; a missing object is the outcome that was asked for.
        }
    }

    public void Dispose()
    {
        if (_client.IsValueCreated)
        {
            _client.Value.Dispose();
        }
    }

    private IAmazonS3 CreateClient()
    {
        var settings = Settings;
        var config = new AmazonS3Config
        {
            ServiceURL = settings.ServiceUrl,
            AuthenticationRegion = settings.Region,

            // B2 and MinIO both serve buckets under the endpoint path, not as subdomains.
            ForcePathStyle = true,

            // AWS SDK v4 attaches CRC checksums by default. Several S3-compatible providers,
            // Backblaze B2 among them, have rejected those headers with HTTP 400. Asking for
            // checksums only where an operation requires them keeps this adapter portable.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            Timeout = TimeSpan.FromSeconds(30),
        };

        return new AmazonS3Client(new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey), config);
    }

    /// <summary>
    /// The provider's own record of which parts landed. Authoritative on purpose: the browser
    /// reports nothing that is trusted, and reading ETags here means the bucket never has to
    /// expose ETag to script over CORS.
    /// </summary>
    private async Task<List<PartETag>> ListPartsAsync(string objectKey, string uploadId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadId);

        var parts = new List<PartETag>();
        string? marker = null;
        do
        {
            var current = marker;
            var listed = await CallAsync(() => Client.ListPartsAsync(
                new ListPartsRequest
                {
                    BucketName = Settings.BucketName,
                    Key = objectKey,
                    UploadId = uploadId,
                    PartNumberMarker = current,
                },
                ct));

            foreach (var part in listed.Parts ?? [])
            {
                parts.Add(new PartETag(part.PartNumber!.Value, part.ETag));
            }

            marker = listed.IsTruncated == true
                ? listed.NextPartNumberMarker?.ToString(CultureInfo.InvariantCulture)
                : null;
        }
        while (marker is not null);

        parts.Sort((a, b) => Nullable.Compare(a.PartNumber, b.PartNumber));
        return parts;
    }

    /// <summary>Collapses provider faults into one exception type handlers can catch.</summary>
    private static async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (AmazonS3Exception e)
        {
            throw new VideoStorageException($"Video storage request failed (HTTP {(int)e.StatusCode}).", e)
            {
                NotFound = e.StatusCode is HttpStatusCode.NotFound,
            };
        }
        catch (AmazonServiceException e)
        {
            throw new VideoStorageException("Video storage is unreachable.", e);
        }
        catch (OperationCanceledException e)
        {
            throw new VideoStorageException("Video storage request timed out.", e);
        }
    }
}
