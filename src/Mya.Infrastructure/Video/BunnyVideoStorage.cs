using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Mya.Application.Abstractions.System;
using Mya.Application.Abstractions.Media;
namespace Mya.Infrastructure.Streaming;
public sealed class BunnyVideoStorage(HttpClient client, IOptions<BunnySettings> options, IClock clock) : IVideoStorage
{
    private BunnySettings Settings => options.Value;
    public bool IsConfigured => Settings.Enabled;
    public bool OwnsLibrary(long libraryId) => IsConfigured && Settings.LibraryId == libraryId;
    public async Task<string> CreateAsync(string title, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, "", new { title }, ct);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var id = body.RootElement.GetProperty("guid").GetString();
        if (!Guid.TryParse(id, out var parsed)) throw new HttpRequestException("Video provider returned an invalid video identifier.");
        return parsed.ToString("D");
    }
    public UploadCredentials Upload(string externalId)
    {
        var expires = new DateTimeOffset(clock.UtcNow.AddHours(6)).ToUnixTimeSeconds();
        var library = Settings.LibraryId.ToString(CultureInfo.InvariantCulture);
        return new UploadCredentials("https://video.bunnycdn.com/tusupload", externalId, library,
            Hash(library + Settings.ApiKey + expires.ToString(CultureInfo.InvariantCulture) + externalId), expires);
    }
    public PlaybackLink Playback(string externalId)
    {
        var expires = new DateTimeOffset(clock.UtcNow.AddMinutes(15)).ToUnixTimeSeconds();
        var token = Hash(Settings.TokenKey + externalId + expires.ToString(CultureInfo.InvariantCulture));
        return new PlaybackLink($"https://iframe.mediadelivery.net/embed/{Settings.LibraryId}/{externalId}?token={token}&expires={expires}&autoplay=false", expires);
    }
    public async Task<RemoteVideo> GetAsync(string externalId, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, "/" + externalId, null, ct);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var json = body.RootElement;
        var thumbnail = json.TryGetProperty("thumbnailFileName", out var t) ? t.GetString() : null;
        return new RemoteVideo(json.GetProperty("status").GetInt32(), json.TryGetProperty("length", out var duration) ? duration.GetInt32() : 0,
            string.IsNullOrEmpty(thumbnail) ? null : $"https://{Settings.CdnHost}/{externalId}/{Uri.EscapeDataString(thumbnail)}");
    }
    public async Task DeleteAsync(string externalId, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Delete, "/" + externalId, null, ct, allowMissing: true);
    }
    public bool VerifyWebhook(byte[] body, string signature, string version, string algorithm)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(signature);
        if (!IsConfigured || version != "v1" || algorithm != "hmac-sha256" || signature.Length != 64) return false;
        try
        {
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Settings.ReadOnlyApiKey), body);
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature));
        }
        catch (FormatException) { return false; }
    }
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string suffix, object? body, CancellationToken ct, bool allowMissing = false)
    {
        if (!IsConfigured) throw new HttpRequestException("Video provider is not configured.");
        using var request = new HttpRequestMessage(method, $"https://video.bunnycdn.com/library/{Settings.LibraryId}/videos{suffix}");
        request.Headers.Add("AccessKey", Settings.ApiKey);
        if (body is not null) request.Content = JsonContent.Create(body);
        HttpResponseMessage response;
        try { response = await client.SendAsync(request, ct); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new HttpRequestException("Video provider request timed out."); }
        if (response.IsSuccessStatusCode || (allowMissing && response.StatusCode == HttpStatusCode.NotFound)) return response;
        var status = response.StatusCode; response.Dispose();
        throw new HttpRequestException($"Video provider request failed (HTTP {(int)status}).", null, status);
    }
}
