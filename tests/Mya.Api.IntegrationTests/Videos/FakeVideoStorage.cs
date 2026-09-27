using Mya.Application.Abstractions.Media;

namespace Mya.Api.IntegrationTests.Videos;

/// <summary>
/// An in-memory stand-in for S3-compatible storage. It keeps the behaviour the handlers depend
/// on — multipart uploads that only exist once started, objects that only exist once completed,
/// and HEAD returning what was actually stored — so handler tests exercise real decisions.
/// Provider wire behaviour is covered separately against MinIO in <c>S3VideoStorageMinioTests</c>.
/// </summary>
public sealed class FakeVideoStorage : IVideoStorage
{
    private readonly Dictionary<string, (long Size, string ContentType)> _objects = [];
    private readonly Dictionary<string, Upload> _uploads = [];

    public bool IsConfigured { get; set; } = true;

    public long MaxFileBytes { get; set; } = 2L * 1024 * 1024 * 1024;

    public long StorageCapBytes { get; set; } = 9L * 1024 * 1024 * 1024;

    public long PartSizeBytes { get; set; } = 16L * 1024 * 1024;

    public IReadOnlyList<string> AllowedContentTypes { get; } = ["video/mp4", "video/quicktime"];

    public TimeSpan PlaybackLifetime => TimeSpan.FromMinutes(120);

    public TimeSpan UploadLifetime => TimeSpan.FromMinutes(360);

    /// <summary>Multipart uploads started. Proves a replayed create does not start a second one.</summary>
    public int Started { get; private set; }

    public int Aborted { get; private set; }

    public bool FailDelete { get; set; }

    public bool FailBegin { get; set; }

    /// <summary>Size the object ends up with, when it should differ from what was declared.</summary>
    public long? CompletedSizeOverride { get; set; }

    public string? CompletedContentTypeOverride { get; set; }

    public IReadOnlyDictionary<string, (long Size, string ContentType)> Objects => _objects;

    public Task<UploadSession> BeginUploadAsync(string objectKey, string contentType, long sizeBytes, CancellationToken ct)
    {
        if (FailBegin)
        {
            throw new VideoStorageException("Storage unavailable.");
        }

        Started++;
        var uploadId = "upload-" + Started.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var parts = VideoStorageMath.PartCount(sizeBytes, PartSizeBytes);
        _uploads[uploadId] = new Upload(objectKey, contentType, sizeBytes, parts, []);
        return Task.FromResult(new UploadSession(objectKey, uploadId, PartSizeBytes, parts));
    }

    public IReadOnlyList<PartUrl> PresignPartUrls(string objectKey, string uploadId, int fromPart, int toPart) =>
        [.. Enumerable.Range(fromPart, toPart - fromPart + 1)
            .Select(p => new PartUrl(p, $"https://storage.test/{objectKey}?uploadId={uploadId}&partNumber={p}&X-Amz-Signature=test"))];

    /// <summary>Test helper: pretend the browser finished uploading these parts.</summary>
    public void PutParts(string uploadId, params int[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        foreach (var part in parts)
        {
            _uploads[uploadId].Parts.Add(part);
        }
    }

    public Task<IReadOnlyList<int>> ListUploadedPartsAsync(string objectKey, string uploadId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<int>>(_uploads.TryGetValue(uploadId, out var upload) ? [.. upload.Parts.Order()] : []);

    public Task<StoredObject> CompleteUploadAsync(string objectKey, string uploadId, CancellationToken ct)
    {
        if (!_uploads.TryGetValue(uploadId, out var upload))
        {
            throw new VideoStorageException("No such upload.") { NotFound = true };
        }

        _uploads.Remove(uploadId);
        var stored = (CompletedSizeOverride ?? upload.SizeBytes, CompletedContentTypeOverride ?? upload.ContentType);
        _objects[objectKey] = stored;
        return Task.FromResult(new StoredObject(stored.Item1, stored.Item2));
    }

    public Task AbortUploadAsync(string objectKey, string uploadId, CancellationToken ct)
    {
        Aborted++;
        _uploads.Remove(uploadId);
        return Task.CompletedTask;
    }

    public Task<StoredObject?> HeadAsync(string objectKey, CancellationToken ct) =>
        Task.FromResult(_objects.TryGetValue(objectKey, out var o) ? new StoredObject(o.Size, o.ContentType) : null);

    public PlaybackLink PresignGet(string objectKey, TimeSpan lifetime) =>
        new($"https://storage.test/{objectKey}?X-Amz-Expires={(int)lifetime.TotalSeconds}&X-Amz-Signature=test",
            DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds());

    public string PresignPut(string objectKey, TimeSpan lifetime) =>
        $"https://storage.test/{objectKey}?X-Amz-Expires={(int)lifetime.TotalSeconds}&X-Amz-Signature=test&method=PUT";

    public Task DeleteAsync(string objectKey, CancellationToken ct)
    {
        if (FailDelete)
        {
            throw new VideoStorageException("Storage unavailable.");
        }

        _objects.Remove(objectKey);
        return Task.CompletedTask;
    }

    /// <summary>Test helper: put an object directly, as if an upload had already completed.</summary>
    public void Seed(string objectKey, long size, string contentType) => _objects[objectKey] = (size, contentType);

    private sealed record Upload(string ObjectKey, string ContentType, long SizeBytes, int PartCount, HashSet<int> Parts);
}
