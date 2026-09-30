using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mya.Application.Abstractions.System;
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
    private const long OneMegabyte = 1024 * 1024;

    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private ServiceProvider provider = null!;
    private IServiceScope scope = null!;
    private AppDbContext db = null!;
    private readonly Caller caller = new();
    private readonly FakeVideoStorage storage = new();
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
    private static VideoCreateInput Create(string title = "Workout", Guid[]? tags = null, long size = 40 * OneMegabyte, string type = "video/mp4") =>
        new(Input(title, tags), new UploadRequest(type, size));

    /// <summary>Creates a draft and walks it all the way to Ready, the way the browser does.</summary>
    private async Task<Video> ReadyAsync(string key = "create", long size = 40 * OneMegabyte)
    {
        var created = await admin.CreateAsync(Create(size: size), key, Ct);
        created.IsSuccess.ShouldBeTrue();
        var row = await db.Videos.SingleAsync(v => v.Id == created.Value.Id);
        (await admin.CompleteAsync(row.Id, new CompleteUploadInput(false, 90), Ct)).IsSuccess.ShouldBeTrue();
        return row;
    }

    [Fact] public async Task Create_issues_one_presigned_url_per_part_and_no_provider_credential()
    {
        var created = await admin.CreateAsync(Create(size: 40 * OneMegabyte), "create", Ct);
        created.IsSuccess.ShouldBeTrue();
        var ticket = created.Value.Upload.ShouldNotBeNull();
        ticket.PartSizeBytes.ShouldBe(16 * OneMegabyte);
        ticket.PartCount.ShouldBe(3);
        ticket.Parts.Count.ShouldBe(3);
        ticket.Parts.Select(p => p.PartNumber).ShouldBe([1, 2, 3]);
        ticket.UploadedParts.ShouldBeEmpty();
        ticket.ThumbnailUploadUrl.ShouldContain("-poster.jpg");
        ticket.Parts.ShouldAllBe(p => p.Url.Contains("X-Amz-Signature", StringComparison.Ordinal));
    }

    [Fact] public async Task Creation_replay_starts_one_upload_and_rejects_a_changed_payload()
    {
        var created = await admin.CreateAsync(Create(), "create", Ct);
        var replay = await admin.CreateAsync(Create(), "create", Ct);
        replay.Value.Id.ShouldBe(created.Value.Id);
        storage.Started.ShouldBe(1);
        replay.Value.Upload.ShouldNotBeNull();
        (await admin.CreateAsync(Create("Different"), "create", Ct)).Error!.Code.ShouldBe("VIDEO_CONFLICT");
    }

    [Fact] public async Task Only_mp4_and_quicktime_are_accepted()
    {
        (await admin.CreateAsync(Create(type: "video/x-matroska"), "mkv", Ct)).Error!.Code.ShouldBe("VIDEO_FILE_TYPE");
        (await admin.CreateAsync(Create(type: "application/zip"), "zip", Ct)).Error!.Code.ShouldBe("VIDEO_FILE_TYPE");
        (await admin.CreateAsync(Create(type: "video/quicktime"), "mov", Ct)).IsSuccess.ShouldBeTrue();
        var row = await db.Videos.SingleAsync();
        row.ExternalId.ShouldEndWith(".mov");
        storage.Started.ShouldBe(1);
    }

    [Fact] public async Task Oversized_file_is_refused_before_any_url_is_issued()
    {
        storage.MaxFileBytes = 100 * OneMegabyte;
        (await admin.CreateAsync(Create(size: 101 * OneMegabyte), "big", Ct)).Error!.Code.ShouldBe("VIDEO_FILE_TOO_LARGE");
        storage.Started.ShouldBe(0);
        (await db.Videos.CountAsync()).ShouldBe(0);
    }

    [Fact] public async Task Storage_cap_counts_drafts_and_blocks_before_presigning()
    {
        storage.StorageCapBytes = 100 * OneMegabyte;
        (await admin.CreateAsync(Create(size: 60 * OneMegabyte), "first", Ct)).IsSuccess.ShouldBeTrue();

        // The first upload has not completed, but its parts already occupy provider storage.
        (await admin.CreateAsync(Create("Second", size: 60 * OneMegabyte), "second", Ct)).Error!.Code.ShouldBe("VIDEO_STORAGE_FULL");
        storage.Started.ShouldBe(1);
        (await admin.CreateAsync(Create("Second", size: 30 * OneMegabyte), "third", Ct)).IsSuccess.ShouldBeTrue();
    }

    [Fact] public async Task Completion_verifies_the_object_and_only_then_marks_it_ready()
    {
        var created = await admin.CreateAsync(Create(size: 40 * OneMegabyte), "create", Ct);
        var row = await db.Videos.SingleAsync();
        row.Status.ShouldBe(VideoStatus.Uploading);
        row.UploadId.ShouldNotBeNull();

        (await admin.CompleteAsync(row.Id, new CompleteUploadInput(false, 125), Ct)).IsSuccess.ShouldBeTrue();
        row.Status.ShouldBe(VideoStatus.Ready);
        row.UploadId.ShouldBeNull();
        row.SizeBytes.ShouldBe(40 * OneMegabyte);
        row.DurationSeconds.ShouldBe(125);
        row.IsPublished.ShouldBeFalse();
        created.Value.Id.ShouldBe(row.Id);
    }

    [Fact] public async Task A_completed_object_that_is_not_what_was_declared_is_deleted_and_fails()
    {
        await admin.CreateAsync(Create(size: 40 * OneMegabyte), "create", Ct);
        var row = await db.Videos.SingleAsync();
        storage.CompletedSizeOverride = 900 * OneMegabyte;

        (await admin.CompleteAsync(row.Id, new CompleteUploadInput(false, 90), Ct)).Error!.Code.ShouldBe("VIDEO_UPLOAD_MISMATCH");
        row.Status.ShouldBe(VideoStatus.Failed);
        row.UploadId.ShouldBeNull();
        storage.Objects.ShouldNotContainKey(row.ExternalId!);
    }

    [Fact] public async Task A_poster_frame_counts_only_when_the_provider_confirms_it()
    {
        await admin.CreateAsync(Create(), "no-poster", Ct);
        var row = await db.Videos.SingleAsync();

        // The browser claims a thumbnail, but nothing was stored.
        (await admin.CompleteAsync(row.Id, new CompleteUploadInput(true, 60), Ct)).IsSuccess.ShouldBeTrue();
        row.ThumbnailObjectKey.ShouldBeNull();

        var second = await admin.CreateAsync(Create("Second"), "poster", Ct);
        var poster = VideoRules.ThumbnailKey(second.Value.Id);
        storage.Seed(poster, 40_000, "image/jpeg");
        (await admin.CompleteAsync(second.Value.Id, new CompleteUploadInput(true, 60), Ct)).IsSuccess.ShouldBeTrue();
        (await db.Videos.SingleAsync(v => v.Id == second.Value.Id)).ThumbnailObjectKey.ShouldBe(poster);

        // The card gets a presigned poster URL inline; a video without a frame gets null and
        // the library falls back to the branded placeholder.
        var listed = (await query.ListAsync(new(), true, Ct)).Value.Items;
        listed.Single(v => v.Id == second.Value.Id).ThumbnailUrl.ShouldNotBeNull().ShouldContain(poster);
        listed.Single(v => v.Id == row.Id).ThumbnailUrl.ShouldBeNull();
    }

    [Fact] public async Task The_same_file_resumes_and_a_different_file_starts_over()
    {
        await admin.CreateAsync(Create(size: 40 * OneMegabyte), "create", Ct);
        var row = await db.Videos.SingleAsync();
        storage.PutParts(row.UploadId!, 1, 2);

        var resumed = await admin.UploadAsync(row.Id, new UploadRequest("video/mp4", 40 * OneMegabyte), Ct);
        resumed.IsSuccess.ShouldBeTrue();
        resumed.Value.UploadedParts.ShouldBe([1, 2]);
        storage.Started.ShouldBe(1);
        storage.Aborted.ShouldBe(0);

        var replaced = await admin.UploadAsync(row.Id, new UploadRequest("video/quicktime", 20 * OneMegabyte), Ct);
        replaced.IsSuccess.ShouldBeTrue();
        replaced.Value.UploadedParts.ShouldBeEmpty();
        replaced.Value.PartCount.ShouldBe(2);
        storage.Started.ShouldBe(2);
        storage.Aborted.ShouldBe(1);
        row.ExternalId.ShouldEndWith(".mov");
    }

    [Fact] public async Task Aborting_an_upload_releases_the_allowance_and_leaves_a_retryable_draft()
    {
        await admin.CreateAsync(Create(size: 40 * OneMegabyte), "create", Ct);
        var row = await db.Videos.SingleAsync();

        (await admin.AbortAsync(row.Id, Ct)).IsSuccess.ShouldBeTrue();
        storage.Aborted.ShouldBe(1);
        row.Status.ShouldBe(VideoStatus.Failed);
        row.UploadId.ShouldBeNull();

        (await admin.UploadAsync(row.Id, new UploadRequest("video/mp4", 40 * OneMegabyte), Ct)).IsSuccess.ShouldBeTrue();
        row.Status.ShouldBe(VideoStatus.Uploading);
    }

    [Fact] public async Task Provider_failure_while_starting_leaves_a_failed_draft_and_no_upload_id()
    {
        storage.FailBegin = true;
        (await admin.CreateAsync(Create(), "create", Ct)).Error!.Code.ShouldBe("VIDEO_PROVIDER_UNAVAILABLE");
        var row = await db.Videos.SingleAsync();
        row.Status.ShouldBe(VideoStatus.Failed);
        row.UploadId.ShouldBeNull();
    }

    [Fact] public async Task Client_sees_only_ready_published_video_and_cannot_manage_it()
    {
        var row = await ReadyAsync();
        caller.UserId = Roles.Client;
        (await query.DetailAsync(row.Id, false, Ct)).IsFailure.ShouldBeTrue();
        (await query.PlaybackAsync(row.Id, false, Ct)).IsFailure.ShouldBeTrue();
        (await admin.CreateAsync(Create(), "forbidden", Ct)).Error!.Code.ShouldBe("FORBIDDEN");
        caller.UserId = Roles.Admin;
        (await admin.PublishAsync(row.Id, new(row.Revision), true, Ct)).IsSuccess.ShouldBeTrue();
        caller.UserId = Roles.Client;
        (await query.ListAsync(new(), false, Ct)).Value.TotalCount.ShouldBe(1);
        (await query.PlaybackAsync(row.Id, false, Ct)).IsSuccess.ShouldBeTrue();
        (await query.DetailAsync(row.Id, true, Ct)).Error!.Code.ShouldBe("FORBIDDEN");
        var user = await db.Users.SingleAsync(u => u.Id == Roles.Client); user.Status = UserStatus.Suspended; await db.SaveChangesAsync();
        (await query.PlaybackAsync(row.Id, false, Ct)).Error!.Code.ShouldBe("FORBIDDEN");
    }

    [Fact] public async Task A_draft_cannot_be_published_and_playback_is_short_lived()
    {
        await admin.CreateAsync(Create(), "create", Ct);
        var row = await db.Videos.SingleAsync();
        (await admin.PublishAsync(row.Id, new(row.Revision), true, Ct)).Error!.Code.ShouldBe("VIDEO_INVALID");
        (await query.PlaybackAsync(row.Id, true, Ct)).Error!.Code.ShouldBe("VIDEO_INVALID");

        (await admin.CompleteAsync(row.Id, new CompleteUploadInput(false, 60), Ct)).IsSuccess.ShouldBeTrue();
        var link = (await query.PlaybackAsync(row.Id, true, Ct)).Value;
        link.Url.ShouldContain("X-Amz-Expires=7200");
        link.Expires.ShouldBeGreaterThan(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    [Fact] public async Task Unpublishing_stops_new_playback_links()
    {
        var row = await ReadyAsync();
        (await admin.PublishAsync(row.Id, new(row.Revision), true, Ct)).IsSuccess.ShouldBeTrue();
        caller.UserId = Roles.Client;
        (await query.PlaybackAsync(row.Id, false, Ct)).IsSuccess.ShouldBeTrue();
        caller.UserId = Roles.Admin;
        (await admin.PublishAsync(row.Id, new(row.Revision), false, Ct)).IsSuccess.ShouldBeTrue();
        caller.UserId = Roles.Client;
        (await query.PlaybackAsync(row.Id, false, Ct)).Error!.Code.ShouldBe("VIDEO_NOT_FOUND");
    }

    [Fact] public async Task Stale_revision_cannot_overwrite_metadata_or_publish()
    {
        var row = await ReadyAsync(); var original = row.Revision;
        (await admin.UpdateAsync(row.Id, Input("Changed") with { Revision = original }, Ct)).IsSuccess.ShouldBeTrue();
        (await admin.UpdateAsync(row.Id, Input("Stale") with { Revision = original }, Ct)).Error!.Code.ShouldBe("VIDEO_CONFLICT");
        (await admin.PublishAsync(row.Id, new(original), true, Ct)).Error!.Code.ShouldBe("VIDEO_CONFLICT"); row.Title.ShouldBe("Changed");
    }

    [Fact] public async Task Failed_remote_delete_hides_video_and_can_be_retried()
    {
        var row = await ReadyAsync(); await admin.PublishAsync(row.Id, new(row.Revision), true, Ct);
        storage.FailDelete = true; (await admin.DeleteAsync(row.Id, Ct)).IsFailure.ShouldBeTrue();
        row.Status.ShouldBe(VideoStatus.Deleting); row.IsPublished.ShouldBeFalse();
        caller.UserId = Roles.Client; (await query.DetailAsync(row.Id, false, Ct)).IsFailure.ShouldBeTrue(); caller.UserId = Roles.Admin;
        storage.FailDelete = false; (await admin.DeleteAsync(row.Id, Ct)).IsSuccess.ShouldBeTrue(); (await db.Videos.CountAsync()).ShouldBe(0);
        storage.Objects.ShouldBeEmpty();
    }

    [Fact] public async Task Deleting_a_draft_aborts_its_upload_so_parts_stop_being_stored()
    {
        await admin.CreateAsync(Create(), "create", Ct);
        var row = await db.Videos.SingleAsync();
        (await admin.DeleteAsync(row.Id, Ct)).IsSuccess.ShouldBeTrue();
        storage.Aborted.ShouldBe(1);
        (await db.Videos.CountAsync()).ShouldBe(0);
    }

    [Fact] public async Task Refresh_reads_the_object_back_and_never_invents_a_ready_video()
    {
        var created = await admin.CreateAsync(Create(), "create", Ct);
        var row = await db.Videos.SingleAsync();

        // Still uploading and nothing stored: the draft must keep its parts.
        (await processing.RefreshAsync(row.Id, Ct)).IsSuccess.ShouldBeTrue();
        row.Status.ShouldBe(VideoStatus.Uploading);

        storage.Seed(row.ExternalId!, 40 * OneMegabyte, "video/mp4");
        (await processing.RefreshAsync(row.Id, Ct)).IsSuccess.ShouldBeTrue();
        row.Status.ShouldBe(VideoStatus.Ready);
        row.IsPublished.ShouldBeFalse();
        created.Value.Upload.ShouldNotBeNull();
    }

    [Fact] public async Task Summary_reports_storage_used_against_the_cap()
    {
        storage.StorageCapBytes = 500 * OneMegabyte;
        await admin.CreateAsync(Create(size: 40 * OneMegabyte), "a", Ct);
        await admin.CreateAsync(Create("B", size: 60 * OneMegabyte), "b", Ct);

        var summary = (await query.SummaryAsync(Ct)).Value;
        summary.Storage.UsedBytes.ShouldBe(100 * OneMegabyte);
        summary.Storage.CapBytes.ShouldBe(500 * OneMegabyte);
        summary.ProviderConfigured.ShouldBeTrue();
        summary.Total.ShouldBe(2);
    }

    [Fact] public async Task An_admin_created_after_startup_can_upload_immediately()
    {
        // Provider availability is a configuration fact, not something computed per account at
        // startup, so an admin invited later must see the same thing the seeded one sees.
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var later = new AppUser
        {
            Id = "later-admin", UserName = "later@example.test", Email = "later@example.test",
            Status = UserStatus.Active, FirstName = "Νέα", LastName = "Διαχειρίστρια",
        };
        (await users.CreateAsync(later)).Succeeded.ShouldBeTrue();
        (await users.AddToRoleAsync(later, Roles.Admin)).Succeeded.ShouldBeTrue();

        caller.UserId = later.Id;

        var summary = await query.SummaryAsync(Ct);
        summary.IsSuccess.ShouldBeTrue();
        summary.Value.ProviderConfigured.ShouldBeTrue();

        // And they can actually start an upload, not merely see the button enabled.
        (await admin.CreateAsync(Create(), "later-admin-upload", Ct)).IsSuccess.ShouldBeTrue();
    }

    [Fact] public async Task An_admin_who_must_change_their_password_is_not_offered_uploads()
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var invited = new AppUser
        {
            Id = "fresh-admin", UserName = "fresh@example.test", Email = "fresh@example.test",
            Status = UserStatus.Active, FirstName = "Νέος", LastName = "Διαχειριστής",
            MustChangePassword = true,
        };
        (await users.CreateAsync(invited)).Succeeded.ShouldBeTrue();
        (await users.AddToRoleAsync(invited, Roles.Admin)).Succeeded.ShouldBeTrue();

        caller.UserId = invited.Id;
        (await query.SummaryAsync(Ct)).Error!.Code.ShouldBe("FORBIDDEN");
    }

    [Fact] public async Task Nothing_can_be_uploaded_while_storage_is_unconfigured()
    {
        storage.IsConfigured = false;
        (await admin.CreateAsync(Create(), "create", Ct)).Error!.Code.ShouldBe("VIDEO_PROVIDER_UNAVAILABLE");
        (await query.SummaryAsync(Ct)).Value.ProviderConfigured.ShouldBeFalse();
    }

    [Fact] public async Task Tags_ignore_Greek_accents_and_used_tags_cannot_be_deleted()
    {
        var a = (await admin.AddTagAsync(new("Κοιλιακοί"), Ct)).Value;
        var b = (await admin.AddTagAsync(new(" κοιλιακοι "), Ct)).Value; a.Id.ShouldBe(b.Id);
        await admin.CreateAsync(Create(tags: [a.Id]), "tagged", Ct);
        (await admin.DeleteTagAsync(a.Id, Ct)).Error!.Code.ShouldBe("VIDEO_CONFLICT");
        (await query.TagsAsync(false, Ct)).Value.ShouldBeEmpty();
    }

    private sealed class Caller : ICurrentUser { public string? UserId { get; set; } = Roles.Admin; public string? UserAgent => null; public string? IpAddress => null; }
}
