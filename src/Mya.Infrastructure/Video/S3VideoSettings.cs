namespace Mya.Infrastructure.Storage;

/// <summary>
/// S3-compatible object storage for video (ADR-019). Configured for Backblaze B2 today;
/// Cloudflare R2 or MinIO need only different values. Validated on start when Enabled.
/// </summary>
public sealed class S3VideoSettings
{
    /// <summary>Smallest part every S3 implementation accepts for a non-final part.</summary>
    public const long MinimumPartSize = 5L * 1024 * 1024;

    /// <summary>Largest part every S3 implementation accepts.</summary>
    public const long MaximumPartSize = 5L * 1024 * 1024 * 1024;

    /// <summary>S3 allows 10,000 parts per multipart upload.</summary>
    public const int MaximumParts = 10_000;

    public bool Enabled { get; set; }

    /// <summary>Provider endpoint, for example the bucket's B2 S3 endpoint. HTTPS, no path.</summary>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>Signing region. For B2 this is the region inside the endpoint hostname.</summary>
    public string Region { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string BucketName { get; set; } = string.Empty;

    /// <summary>Largest single recording the API will presign. Default 2 GiB.</summary>
    public long MaxFileBytes { get; set; } = 2L * 1024 * 1024 * 1024;

    /// <summary>Total stored originals allowed. Default 9 GiB, under B2's 10 GB free tier.</summary>
    public long StorageCapBytes { get; set; } = 9L * 1024 * 1024 * 1024;

    /// <summary>Multipart part size. Default 16 MiB: a dropped part costs 16 MiB, not the file.</summary>
    public long PartSizeBytes { get; set; } = 16L * 1024 * 1024;

    /// <summary>Lifetime of a playback link. Documented trade-off: shareable until it expires.</summary>
    public int PlaybackMinutes { get; set; } = 120;

    /// <summary>Lifetime of an upload part URL. Long enough for a slow mobile connection.</summary>
    public int UploadMinutes { get; set; } = 360;
}
