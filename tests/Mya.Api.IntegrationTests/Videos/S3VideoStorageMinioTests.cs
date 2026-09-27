using System.Net;
using System.Net.Http.Headers;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Mya.Application.Abstractions.Media;
using Mya.Application.Common.Time;
using Mya.Infrastructure.Storage;
using Shouldly;
using Testcontainers.Minio;

namespace Mya.Api.IntegrationTests.Videos;

/// <summary>
/// The adapter against a real S3 implementation. MinIO stands in for Backblaze B2: it speaks the
/// same S3 API, so the multipart round trip, the presigned URLs the browser actually uses, HEAD
/// verification and deletion are exercised over the wire rather than against a stub. B2's own
/// behaviour still needs the live acceptance run in docs/11; this proves the adapter, not B2.
/// </summary>
public sealed class S3VideoStorageMinioTests : IAsyncLifetime
{
    private const string Bucket = "videos";
    private const long PartSize = 5L * 1024 * 1024;

    // MinIO's own Docker Hub repository now requires authentication, so CI and a clean laptop
    // cannot pull minio/minio anonymously. Chainguard publishes an equivalent public build.
    private const string MinioImage = "chainguard/minio:latest";

    private readonly MinioContainer _minio = new MinioBuilder(MinioImage).Build();
    private static readonly HttpClient Browser = new();
    private AmazonS3Client _client = null!;
    private S3VideoStorage _storage = null!;

    public async Task InitializeAsync()
    {
        await _minio.StartAsync();

        var endpoint = _minio.GetConnectionString();
        _client = new AmazonS3Client(
            new BasicAWSCredentials(_minio.GetAccessKey(), _minio.GetSecretKey()),
            new AmazonS3Config
            {
                ServiceURL = endpoint.StartsWith("http", StringComparison.Ordinal) ? endpoint : "http://" + endpoint,
                AuthenticationRegion = "us-east-1",
                ForcePathStyle = true,

                // The container speaks plain HTTP. Without this the SDK presigns https URLs.
                // Production always uses an HTTPS endpoint, which startup validation enforces.
                UseHttp = true,

                // The same settings the production client uses. If they were wrong for an
                // S3-compatible store, these tests would fail the way B2 would.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });

        await _client.PutBucketAsync(new PutBucketRequest { BucketName = Bucket });

        var settings = new S3VideoSettings
        {
            Enabled = true,
            BucketName = Bucket,
            Region = "us-east-1",
            PartSizeBytes = PartSize,
            MaxFileBytes = 64L * 1024 * 1024,
            StorageCapBytes = 128L * 1024 * 1024,
            PlaybackMinutes = 120,
            UploadMinutes = 60,
        };
        _storage = new S3VideoStorage(Options.Create(settings), new UtcClock(), _client);
    }

    public async Task DisposeAsync()
    {
        _storage.Dispose();
        _client.Dispose();
        await _minio.DisposeAsync();
    }

    [Fact]
    public async Task A_browser_uploads_every_part_and_the_server_completes_and_verifies_the_object()
    {
        // Two parts: the first exactly at the minimum part size, the second a short tail.
        var bytes = Bytes((int)PartSize + 1024);
        var key = "videos/round-trip.mp4";

        var session = await _storage.BeginUploadAsync(key, "video/mp4", bytes.Length, CancellationToken.None);
        session.PartCount.ShouldBe(2);
        session.PartSizeBytes.ShouldBe(PartSize);

        var urls = _storage.PresignPartUrls(key, session.UploadId, 1, session.PartCount);
        urls.Count.ShouldBe(2);
        foreach (var url in urls)
        {
            // No credential may ever reach the browser, only a signature.
            url.Url.ShouldNotContain(_minio.GetSecretKey());
            url.Url.ShouldContain("X-Amz-Signature");
        }

        await PutPartAsync(urls[0].Url, bytes.AsMemory(0, (int)PartSize));
        (await _storage.ListUploadedPartsAsync(key, session.UploadId, CancellationToken.None)).ShouldBe([1]);

        await PutPartAsync(urls[1].Url, bytes.AsMemory((int)PartSize));
        (await _storage.ListUploadedPartsAsync(key, session.UploadId, CancellationToken.None)).ShouldBe([1, 2]);

        var stored = await _storage.CompleteUploadAsync(key, session.UploadId, CancellationToken.None);
        stored.SizeBytes.ShouldBe(bytes.Length);
        stored.ContentType.ShouldBe("video/mp4");

        var head = await _storage.HeadAsync(key, CancellationToken.None);
        head.ShouldNotBeNull();
        head.SizeBytes.ShouldBe(bytes.Length);
    }

    [Fact]
    public async Task A_presigned_get_plays_the_object_back_and_supports_range_requests()
    {
        var bytes = Bytes(2048);
        var key = "videos/playback.mp4";
        await UploadAsync(key, bytes);

        var link = _storage.PresignGet(key, TimeSpan.FromMinutes(120));
        link.Url.ShouldContain("X-Amz-Signature");

        using var whole = await Browser.GetAsync(new Uri(link.Url));
        whole.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await whole.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);

        // A native <video> element seeks with Range; without 206 the player cannot scrub.
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(link.Url));
        request.Headers.Range = new RangeHeaderValue(100, 199);
        using var partial = await Browser.SendAsync(request);
        partial.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        (await partial.Content.ReadAsByteArrayAsync()).Length.ShouldBe(100);
    }

    [Fact]
    public async Task A_presigned_put_stores_the_poster_frame_the_browser_captured()
    {
        var key = "videos/poster-test-poster.jpg";
        var url = _storage.PresignPut(key, TimeSpan.FromMinutes(60));

        using var content = new ByteArrayContent(Bytes(512));
        using var response = await Browser.PutAsync(new Uri(url), content);
        response.IsSuccessStatusCode.ShouldBeTrue();

        var head = await _storage.HeadAsync(key, CancellationToken.None);
        head.ShouldNotBeNull();
        head.SizeBytes.ShouldBe(512);
    }

    [Fact]
    public async Task An_aborted_upload_leaves_nothing_behind_and_aborting_twice_is_safe()
    {
        var key = "videos/aborted.mp4";
        var session = await _storage.BeginUploadAsync(key, "video/mp4", PartSize, CancellationToken.None);
        var urls = _storage.PresignPartUrls(key, session.UploadId, 1, 1);
        await PutPartAsync(urls[0].Url, Bytes((int)PartSize).AsMemory());

        await _storage.AbortUploadAsync(key, session.UploadId, CancellationToken.None);
        (await _storage.HeadAsync(key, CancellationToken.None)).ShouldBeNull();

        // Already gone is the outcome we wanted, not a failure to report.
        await Should.NotThrowAsync(() => _storage.AbortUploadAsync(key, session.UploadId, CancellationToken.None));
    }

    [Fact]
    public async Task Head_and_delete_treat_a_missing_object_as_absent_rather_than_an_error()
    {
        (await _storage.HeadAsync("videos/never-existed.mp4", CancellationToken.None)).ShouldBeNull();
        await Should.NotThrowAsync(() => _storage.DeleteAsync("videos/never-existed.mp4", CancellationToken.None));
    }

    [Fact]
    public async Task Deleting_removes_the_object()
    {
        var key = "videos/deleted.mp4";
        await UploadAsync(key, Bytes(1024));
        (await _storage.HeadAsync(key, CancellationToken.None)).ShouldNotBeNull();

        await _storage.DeleteAsync(key, CancellationToken.None);
        (await _storage.HeadAsync(key, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Completing_an_upload_with_no_parts_fails_rather_than_creating_an_empty_object()
    {
        var key = "videos/empty.mp4";
        var session = await _storage.BeginUploadAsync(key, "video/mp4", 1024, CancellationToken.None);

        await Should.ThrowAsync<VideoStorageException>(
            () => _storage.CompleteUploadAsync(key, session.UploadId, CancellationToken.None));
        (await _storage.HeadAsync(key, CancellationToken.None)).ShouldBeNull();
    }

    private static byte[] Bytes(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)(i % 251);
        }

        return bytes;
    }

    private async Task UploadAsync(string key, byte[] bytes)
    {
        var session = await _storage.BeginUploadAsync(key, "video/mp4", bytes.Length, CancellationToken.None);
        var urls = _storage.PresignPartUrls(key, session.UploadId, 1, session.PartCount);
        await PutPartAsync(urls[0].Url, bytes.AsMemory());
        await _storage.CompleteUploadAsync(key, session.UploadId, CancellationToken.None);
    }

    /// <summary>Exactly what the browser does: a plain PUT of a slice to a presigned URL.</summary>
    private static async Task PutPartAsync(string url, ReadOnlyMemory<byte> slice)
    {
        using var content = new ReadOnlyMemoryContent(slice);
        using var response = await Browser.PutAsync(new Uri(url), content);
        response.IsSuccessStatusCode.ShouldBeTrue($"part upload returned {(int)response.StatusCode}");
    }
}
