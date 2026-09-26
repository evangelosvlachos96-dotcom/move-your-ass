using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mya.Application.Abstractions.System;
using Mya.Application.Abstractions.Media;
using Mya.Application.Common.Time;
using Mya.Application.Features.Videos;
using Mya.Domain.Constants;
using Mya.Domain.Entities;
using Mya.Domain.Enums;
using Mya.Infrastructure;
using Mya.Infrastructure.Identity;
using Mya.Infrastructure.Persistence;
using Shouldly;
namespace Mya.Api.IntegrationTests.Videos;
public sealed class VideoWorkflowTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private ServiceProvider provider = null!;
    private IServiceScope scope = null!;
    private AppDbContext db = null!;
    private readonly Caller caller = new();
    private readonly Storage storage = new();
    private VideoAdminHandler admin = null!;
    private VideoQueryHandler query = null!;
    private VideoProcessingHandler processing = null!;
    private static readonly CancellationToken Ct = CancellationToken.None;
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        var services = new ServiceCollection(); services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection)); services.AddIdentityStores();
        provider = services.BuildServiceProvider(); scope = provider.CreateScope();
        db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.EnsureCreatedAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole(Roles.Admin)); await roles.CreateAsync(new IdentityRole(Roles.Client));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        foreach (var role in new[] { Roles.Admin, Roles.Client })
        {
            var user = new AppUser { Id = role, UserName = role + "@example.test", Email = role + "@example.test", Status = UserStatus.Active, FirstName = "Test", LastName = role };
            (await users.CreateAsync(user)).Succeeded.ShouldBeTrue(); (await users.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
        }
        var clock = new UtcClock(); var access = new VideoAccess(new UserService(users, db, clock), caller);
        admin = new(db, access, caller, clock, storage); query = new(db, access, storage); processing = new(db, storage, access, clock);
    }
    public async Task DisposeAsync() { scope.Dispose(); await provider.DisposeAsync(); await connection.DisposeAsync(); }
    private static VideoInput Input(string title = "Workout", Guid[]? tags = null) => new(title, "Description", VideoAudience.Both, VideoBodyArea.FullBody, false, tags ?? [], null);
    private async Task<Video> CreateAsync(string key = "create")
    {
        var result = await admin.CreateAsync(Input(), key, Ct); result.IsSuccess.ShouldBeTrue();
        return await db.Videos.SingleAsync(v => v.Id == result.Value.Id);
    }
    [Fact] public async Task Creation_replay_creates_one_remote_asset_and_rejects_changed_payload()
    {
        var row = await CreateAsync(); var replay = await admin.CreateAsync(Input(), "create", Ct);
        replay.Value.Id.ShouldBe(row.Id); storage.Created.ShouldBe(1);
        (await admin.CreateAsync(Input("Different"), "create", Ct)).Error!.Code.ShouldBe("VIDEO_CONFLICT");
    }
    [Fact] public async Task Client_sees_only_ready_published_video_and_cannot_manage_it()
    {
        var row = await CreateAsync(); caller.UserId = Roles.Client;
        (await query.DetailAsync(row.Id, false, Ct)).IsFailure.ShouldBeTrue();
        (await query.PlaybackAsync(row.Id, false, Ct)).IsFailure.ShouldBeTrue();
        (await admin.CreateAsync(Input(), "forbidden", Ct)).Error!.Code.ShouldBe("FORBIDDEN");
        caller.UserId = Roles.Admin;
        (await admin.PublishAsync(row.Id, new(row.Revision), true, Ct)).IsFailure.ShouldBeTrue();
        (await processing.RefreshAsync(row.Id, Ct)).IsSuccess.ShouldBeTrue();
        row.IsPublished.ShouldBeFalse();
        (await admin.PublishAsync(row.Id, new(row.Revision), true, Ct)).IsSuccess.ShouldBeTrue();
        caller.UserId = Roles.Client;
        (await query.ListAsync(new(), false, Ct)).Value.TotalCount.ShouldBe(1);
        (await query.PlaybackAsync(row.Id, false, Ct)).IsSuccess.ShouldBeTrue();
        (await query.DetailAsync(row.Id, true, Ct)).Error!.Code.ShouldBe("FORBIDDEN");
        var user = await db.Users.SingleAsync(u => u.Id == Roles.Client); user.Status = UserStatus.Suspended; await db.SaveChangesAsync();
        (await query.PlaybackAsync(row.Id, false, Ct)).Error!.Code.ShouldBe("FORBIDDEN");
    }
    [Fact] public async Task Stale_revision_cannot_overwrite_metadata_or_publish()
    {
        var row = await CreateAsync(); var original = row.Revision;
        (await admin.UpdateAsync(row.Id, Input("Changed") with { Revision = original }, Ct)).IsSuccess.ShouldBeTrue();
        (await admin.UpdateAsync(row.Id, Input("Stale") with { Revision = original }, Ct)).Error!.Code.ShouldBe("VIDEO_CONFLICT");
        (await admin.PublishAsync(row.Id, new(original), true, Ct)).Error!.Code.ShouldBe("VIDEO_CONFLICT"); row.Title.ShouldBe("Changed");
    }
    [Fact] public async Task Failed_remote_delete_hides_video_and_can_be_retried()
    {
        var row = await CreateAsync(); await processing.RefreshAsync(row.Id, Ct); await admin.PublishAsync(row.Id, new(row.Revision), true, Ct);
        storage.FailDelete = true; (await admin.DeleteAsync(row.Id, Ct)).IsFailure.ShouldBeTrue();
        row.Status.ShouldBe(VideoStatus.Deleting); row.IsPublished.ShouldBeFalse();
        caller.UserId = Roles.Client; (await query.DetailAsync(row.Id, false, Ct)).IsFailure.ShouldBeTrue(); caller.UserId = Roles.Admin;
        storage.FailDelete = false; (await admin.DeleteAsync(row.Id, Ct)).IsSuccess.ShouldBeTrue(); (await db.Videos.CountAsync()).ShouldBe(0);
    }
    [Fact] public async Task Tags_ignore_Greek_accents_and_used_tags_cannot_be_deleted()
    {
        var a = (await admin.AddTagAsync(new("Κοιλιακοί"), Ct)).Value;
        var b = (await admin.AddTagAsync(new(" κοιλιακοι "), Ct)).Value; a.Id.ShouldBe(b.Id);
        await admin.CreateAsync(Input(tags: [a.Id]), "tagged", Ct);
        (await admin.DeleteTagAsync(a.Id, Ct)).Error!.Code.ShouldBe("VIDEO_CONFLICT");
        (await query.TagsAsync(false, Ct)).Value.ShouldBeEmpty();
    }
    [Theory] [InlineData("[]")] [InlineData("{\"VideoLibraryId\":\"wrong\",\"VideoGuid\":4}")]
    public async Task Malformed_signed_webhook_is_rejected_without_exception(string json)
    {
        (await processing.WebhookAsync(System.Text.Encoding.UTF8.GetBytes(json), "signed", "v1", "hmac-sha256", Ct)).Error!.Code.ShouldBe("VIDEO_INVALID");
    }
    [Fact] public async Task Replayed_webhook_reads_current_provider_state_and_never_publishes()
    {
        var row = await CreateAsync(); var bytes = System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new { VideoLibraryId = 1, VideoGuid = row.ExternalId, Status = 1 }));
        (await processing.WebhookAsync(bytes, "signed", "v1", "hmac-sha256", Ct)).IsSuccess.ShouldBeTrue();
        row.Status.ShouldBe(VideoStatus.Ready); row.IsPublished.ShouldBeFalse();
        (await processing.WebhookAsync(bytes, "signed", "v1", "hmac-sha256", Ct)).IsSuccess.ShouldBeTrue(); row.Status.ShouldBe(VideoStatus.Ready);
        (await processing.WebhookAsync(bytes, "invalid", "v1", "hmac-sha256", Ct)).Error!.Code.ShouldBe("WEBHOOK_INVALID");
    }
    private sealed class Caller : ICurrentUser { public string? UserId { get; set; } = Roles.Admin; public string? UserAgent => null; public string? IpAddress => null; }
    private sealed class Storage : IVideoStorage
    {
        public bool IsConfigured => true; public int Created { get; private set; } public bool FailDelete { get; set; }
        public Task<string> CreateAsync(string title, CancellationToken ct) { Created++; return Task.FromResult(Guid.NewGuid().ToString()); }
        public UploadCredentials Upload(string id) => new("https://video.bunnycdn.com/tusupload", id, "1", "test", 1);
        public PlaybackLink Playback(string id) => new("https://iframe.mediadelivery.net/embed/1/" + id, 1);
        public Task<RemoteVideo> GetAsync(string id, CancellationToken ct) => Task.FromResult(new RemoteVideo(3, 60, null));
        public Task DeleteAsync(string id, CancellationToken ct) => FailDelete ? Task.FromException(new HttpRequestException("Unavailable")) : Task.CompletedTask;
        public bool VerifyWebhook(byte[] body, string signature, string version, string algorithm) => signature == "signed";
        public bool OwnsLibrary(long id) => id == 1;
    }
}
