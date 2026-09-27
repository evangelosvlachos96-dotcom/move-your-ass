namespace Mya.Application.Abstractions.Media;

/// <summary>A started multipart upload. The browser PUTs parts; the server completes it.</summary>
public sealed record UploadSession(string ObjectKey, string UploadId, long PartSizeBytes, int PartCount);

/// <summary>One presigned PUT URL for a single part. Never carries a provider credential.</summary>
public sealed record PartUrl(int PartNumber, string Url);

/// <summary>What the provider reports about a stored object, read back with HEAD.</summary>
public sealed record StoredObject(long SizeBytes, string ContentType);

/// <summary>A short-lived, presigned GET URL. Shareable until <see cref="Expires"/>; see ADR-019.</summary>
public sealed record PlaybackLink(string Url, long Expires);

/// <summary>
/// Object storage for video originals and thumbnails, behind an S3-shaped contract (ADR-019).
/// Provider-neutral on purpose: Backblaze B2 today, Cloudflare R2 or MinIO by configuration only.
/// No bytes pass through the API — the browser talks to the provider with presigned URLs.
/// </summary>
public interface IVideoStorage
{
    public bool IsConfigured { get; }

    /// <summary>Largest single upload the API will presign, in bytes.</summary>
    public long MaxFileBytes { get; }

    /// <summary>Total bytes allowed across every stored original, in bytes.</summary>
    public long StorageCapBytes { get; }

    /// <summary>Multipart part size, in bytes. A failed part costs one part, not the file.</summary>
    public long PartSizeBytes { get; }

    /// <summary>Content types accepted for an original recording.</summary>
    public IReadOnlyList<string> AllowedContentTypes { get; }

    /// <summary>How long a playback link stays valid. Configuration, not a handler decision.</summary>
    public TimeSpan PlaybackLifetime { get; }

    /// <summary>How long an upload URL stays valid, sized for a slow mobile connection.</summary>
    public TimeSpan UploadLifetime { get; }

    public Task<UploadSession> BeginUploadAsync(string objectKey, string contentType, long sizeBytes, CancellationToken ct);

    /// <summary>Presigned PUT URLs for parts <paramref name="fromPart"/>..<paramref name="toPart"/>, inclusive and 1-based.</summary>
    public IReadOnlyList<PartUrl> PresignPartUrls(string objectKey, string uploadId, int fromPart, int toPart);

    /// <summary>Part numbers the provider already holds, so a reload resumes instead of restarting.</summary>
    public Task<IReadOnlyList<int>> ListUploadedPartsAsync(string objectKey, string uploadId, CancellationToken ct);

    /// <summary>Lists the parts the provider actually holds and completes the upload from them.</summary>
    public Task<StoredObject> CompleteUploadAsync(string objectKey, string uploadId, CancellationToken ct);

    /// <summary>Discards an unfinished upload so its parts stop being billed. Missing counts as aborted.</summary>
    public Task AbortUploadAsync(string objectKey, string uploadId, CancellationToken ct);

    /// <summary>HEAD the object. Null when it does not exist.</summary>
    public Task<StoredObject?> HeadAsync(string objectKey, CancellationToken ct);

    public PlaybackLink PresignGet(string objectKey, TimeSpan lifetime);

    public string PresignPut(string objectKey, TimeSpan lifetime);

    /// <summary>Deletes an object. Already-missing counts as deleted.</summary>
    public Task DeleteAsync(string objectKey, CancellationToken ct);
}
