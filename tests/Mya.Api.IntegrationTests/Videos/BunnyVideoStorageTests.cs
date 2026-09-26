using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Mya.Application.Abstractions.System;
using Mya.Infrastructure.Streaming;
using Shouldly;
namespace Mya.Api.IntegrationTests.Videos;
public sealed class BunnyVideoStorageTests
{
    private static BunnySettings Settings => new() { Enabled = true, LibraryId = 123, ApiKey = "synthetic-write-key", ReadOnlyApiKey = "synthetic-read-key", TokenKey = "synthetic-token-key", CdnHost = "test.b-cdn.net" };
    private static BunnyVideoStorage Storage(HttpMessageHandler? handler = null) => new(new HttpClient(handler ?? new Reply(HttpStatusCode.OK, "{}")), Options.Create(Settings), new Clock());
    [Fact] public void Signature_is_bound_to_exact_body_and_signature_version()
    {
        var body = Encoding.UTF8.GetBytes("{ \"VideoLibraryId\": 123 }".Replace("\\", ""));
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Settings.ReadOnlyApiKey), body));
        var storage = Storage(); storage.VerifyWebhook(body, signature, "v1", "hmac-sha256").ShouldBeTrue();
        storage.VerifyWebhook([1,2,3], signature, "v1", "hmac-sha256").ShouldBeFalse();
        storage.VerifyWebhook(body, signature, "v2", "hmac-sha256").ShouldBeFalse();
        storage.VerifyWebhook(body, new string('z',64), "v1", "hmac-sha256").ShouldBeFalse();
    }
    [Fact] public void Temporary_credentials_do_not_expose_provider_keys()
    {
        var storage = Storage(); var id = Guid.NewGuid().ToString(); var upload = storage.Upload(id); var playback = storage.Playback(id);
        upload.Endpoint.ShouldBe("https://video.bunnycdn.com/tusupload"); upload.Signature.Length.ShouldBe(64);
        (upload.Expires - new DateTimeOffset(Clock.Now).ToUnixTimeSeconds()).ShouldBe(21600);
        (playback.Expires - new DateTimeOffset(Clock.Now).ToUnixTimeSeconds()).ShouldBe(900);
        playback.Url.ShouldNotContain(Settings.TokenKey); upload.Signature.ShouldNotContain(Settings.ApiKey);
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Settings.TokenKey + id + playback.Expires)));
        playback.Url.ShouldContain("token=" + expected);
    }
    [Fact] public async Task Provider_failure_does_not_echo_response_body()
    {
        var ex = await Should.ThrowAsync<HttpRequestException>(() => Storage(new Reply(HttpStatusCode.BadRequest, "private-provider-response")).CreateAsync("Title", CancellationToken.None));
        ex.Message.ShouldNotContain("private-provider-response"); ex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
    [Fact] public async Task Remote_delete_tolerates_already_missing_asset() => await Storage(new Reply(HttpStatusCode.NotFound, "")).DeleteAsync(Guid.NewGuid().ToString(), CancellationToken.None);
    private sealed class Clock : IClock { public static readonly DateTime Now = new(2026,9,26,12,0,0,DateTimeKind.Utc); public DateTime UtcNow => Now; }
    private sealed class Reply(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
