namespace Mya.Application.Abstractions.Media;
public sealed record UploadCredentials(string Endpoint, string VideoId, string LibraryId, string Signature, long Expires);
public sealed record PlaybackLink(string Url, long Expires);
public sealed record RemoteVideo(int Status, int DurationSeconds, string? ThumbnailUrl);
public interface IVideoStorage
{
    public bool IsConfigured { get; }
    public Task<string> CreateAsync(string title, CancellationToken ct);
    public UploadCredentials Upload(string externalId);
    public PlaybackLink Playback(string externalId);
    public Task<RemoteVideo> GetAsync(string externalId, CancellationToken ct);
    public Task DeleteAsync(string externalId, CancellationToken ct);
    public bool VerifyWebhook(byte[] body, string signature, string version, string algorithm);
    public bool OwnsLibrary(long libraryId);
}
