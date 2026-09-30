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

/// <summary>
/// Custom cover images: what is accepted, what the fallback order is, and that replacing or
/// deleting one does not leave the old picture paying rent in the bucket.
/// </summary>
public sealed class VideoCoverTests : IAsyncLifetime
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
    private static readonly CancellationToken Ct = CancellationToken.None;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        services.AddIdentityStores();
        provider = services.BuildServiceProvider();
        scope = provider.CreateScope();
        db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole(Roles.Admin));
        await roles.CreateAsync(new IdentityRole(Roles.Client));

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        foreach (var role in new[] { Roles.Admin, Roles.Client })
        {
            var user = new AppUser
            {
                Id = role, UserName = role + "@example.test", Email = role + "@example.test",
                Status = UserStatus.Active, FirstName = "Test", LastName = role,
            };
            (await users.CreateAsync(user)).Succeeded.ShouldBeTrue();
            (await users.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
        }

        var clock = new UtcClock();
        var access = new VideoAccess(new UserService(users, db, clock), caller);
        admin = new(db, access, caller, clock, storage);
        query = new(db, access, storage);
    }

    public async Task DisposeAsync()
    {
        scope.Dispose();
        await provider.DisposeAsync();
        await connection.DisposeAsync();
    }

    private static VideoCreateInput Create(string title = "Workout") =>
        new(new VideoInput(title, "Description", VideoAudience.Both, VideoBodyArea.FullBody, false, [], null),
            new UploadRequest("video/mp4", 40 * OneMegabyte));

    /// <summary>A Ready video, optionally with an auto-captured poster frame already stored.</summary>
    private async Task<Video> ReadyAsync(string key = "create", bool withPoster = false)
    {
        var created = await admin.CreateAsync(Create(), key, Ct);
        created.IsSuccess.ShouldBeTrue();
        var row = await db.Videos.SingleAsync(v => v.Id == created.Value.Id);

        if (withPoster)
        {
            storage.Seed(VideoRules.ThumbnailKey(row.Id), 40_000, "image/jpeg");
        }

        (await admin.CompleteAsync(row.Id, new CompleteUploadInput(withPoster, 90), Ct)).IsSuccess.ShouldBeTrue();
        return row;
    }

    /// <summary>Walks a cover all the way through: ticket, upload, confirm.</summary>
    private async Task<string> AttachCoverAsync(Guid id, string type = "image/jpeg", long size = 120_000)
    {
        var ticket = await admin.CoverUploadAsync(id, new CoverRequest(type, size), Ct);
        ticket.IsSuccess.ShouldBeTrue();
        storage.Seed(ticket.Value.ObjectKey, size, type);
        (await admin.CoverConfirmAsync(id, new CoverConfirm(ticket.Value.ObjectKey), Ct)).IsSuccess.ShouldBeTrue();
        return ticket.Value.ObjectKey;
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public async Task The_three_picture_formats_are_accepted(string type)
    {
        var row = await ReadyAsync();
        (await admin.CoverUploadAsync(row.Id, new CoverRequest(type, 100_000), Ct)).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/svg+xml")]
    [InlineData("application/pdf")]
    [InlineData("video/mp4")]
    public async Task Anything_else_is_refused_before_a_url_exists(string type)
    {
        var row = await ReadyAsync();
        (await admin.CoverUploadAsync(row.Id, new CoverRequest(type, 100_000), Ct)).Error!.Code.ShouldBe("VIDEO_COVER_TYPE");
    }

    [Fact]
    public async Task A_cover_over_five_megabytes_is_refused()
    {
        var row = await ReadyAsync();
        (await admin.CoverUploadAsync(row.Id, new CoverRequest("image/jpeg", 6 * OneMegabyte), Ct))
            .Error!.Code.ShouldBe("VIDEO_COVER_TOO_LARGE");
        (await admin.CoverUploadAsync(row.Id, new CoverRequest("image/jpeg", 0), Ct))
            .Error!.Code.ShouldBe("VIDEO_COVER_TOO_LARGE");
    }

    [Fact]
    public async Task A_cover_the_provider_says_is_the_wrong_type_is_rejected_and_deleted()
    {
        var row = await ReadyAsync();
        var ticket = await admin.CoverUploadAsync(row.Id, new CoverRequest("image/jpeg", 100_000), Ct);

        // The browser uploaded something other than what it declared.
        storage.Seed(ticket.Value.ObjectKey, 100_000, "application/pdf");

        (await admin.CoverConfirmAsync(row.Id, new CoverConfirm(ticket.Value.ObjectKey), Ct))
            .Error!.Code.ShouldBe("VIDEO_COVER_TYPE");
        storage.Objects.ShouldNotContainKey(ticket.Value.ObjectKey);
        (await db.Videos.SingleAsync(v => v.Id == row.Id)).CoverObjectKey.ShouldBeNull();
    }

    [Fact]
    public async Task A_key_belonging_to_another_video_cannot_be_adopted()
    {
        var mine = await ReadyAsync("mine");
        var theirs = await ReadyAsync("theirs");
        var ticket = await admin.CoverUploadAsync(theirs.Id, new CoverRequest("image/jpeg", 100_000), Ct);
        storage.Seed(ticket.Value.ObjectKey, 100_000, "image/jpeg");

        (await admin.CoverConfirmAsync(mine.Id, new CoverConfirm(ticket.Value.ObjectKey), Ct))
            .Error!.Code.ShouldBe("VIDEO_INVALID");
    }

    [Fact]
    public async Task The_cover_wins_over_the_captured_frame_and_removing_it_falls_back()
    {
        var row = await ReadyAsync("fallback", withPoster: true);
        var poster = VideoRules.ThumbnailKey(row.Id);

        // With only a captured frame, that is what the card shows.
        var beforeCover = (await query.DetailAsync(row.Id, true, Ct)).Value;
        beforeCover.ThumbnailUrl.ShouldNotBeNull().ShouldContain(poster);
        beforeCover.HasCustomCover.ShouldBeFalse();

        var cover = await AttachCoverAsync(row.Id);
        var withCover = (await query.DetailAsync(row.Id, true, Ct)).Value;
        withCover.ThumbnailUrl.ShouldNotBeNull().ShouldContain(cover);
        withCover.HasCustomCover.ShouldBeTrue();

        // Removing it falls back to the frame rather than to nothing.
        (await admin.CoverRemoveAsync(row.Id, Ct)).IsSuccess.ShouldBeTrue();
        var afterRemove = (await query.DetailAsync(row.Id, true, Ct)).Value;
        afterRemove.ThumbnailUrl.ShouldNotBeNull().ShouldContain(poster);
        afterRemove.HasCustomCover.ShouldBeFalse();
    }

    [Fact]
    public async Task With_neither_a_cover_nor_a_frame_there_is_no_url_and_the_ui_shows_the_placeholder()
    {
        var row = await ReadyAsync();
        (await query.DetailAsync(row.Id, true, Ct)).Value.ThumbnailUrl.ShouldBeNull();
    }

    [Fact]
    public async Task Replacing_a_cover_deletes_the_one_it_replaced()
    {
        var row = await ReadyAsync();
        var first = await AttachCoverAsync(row.Id);
        var second = await AttachCoverAsync(row.Id, "image/webp", 90_000);

        first.ShouldNotBe(second);
        storage.Objects.ShouldNotContainKey(first);
        storage.Objects.ShouldContainKey(second);
        (await db.Videos.SingleAsync(v => v.Id == row.Id)).CoverObjectKey.ShouldBe(second);
    }

    [Fact]
    public async Task Removing_a_cover_deletes_its_object()
    {
        var row = await ReadyAsync();
        var cover = await AttachCoverAsync(row.Id);

        (await admin.CoverRemoveAsync(row.Id, Ct)).IsSuccess.ShouldBeTrue();
        storage.Objects.ShouldNotContainKey(cover);
        var stored = await db.Videos.SingleAsync(v => v.Id == row.Id);
        stored.CoverObjectKey.ShouldBeNull();
        stored.CoverSizeBytes.ShouldBeNull();
    }

    [Fact]
    public async Task Deleting_the_video_takes_the_cover_and_the_frame_with_it()
    {
        var row = await ReadyAsync("delete", withPoster: true);
        var cover = await AttachCoverAsync(row.Id);
        var poster = VideoRules.ThumbnailKey(row.Id);

        (await admin.DeleteAsync(row.Id, Ct)).IsSuccess.ShouldBeTrue();

        storage.Objects.ShouldNotContainKey(cover);
        storage.Objects.ShouldNotContainKey(poster);
        storage.Objects.ShouldBeEmpty();
    }

    [Fact]
    public async Task Covers_and_frames_count_towards_the_storage_cap()
    {
        storage.StorageCapBytes = 100 * OneMegabyte;
        var row = await ReadyAsync("cap", withPoster: true);
        await AttachCoverAsync(row.Id, "image/jpeg", 2 * OneMegabyte);

        var used = (await query.SummaryAsync(Ct)).Value.Storage.UsedBytes;
        // 40 MB recording + 40 KB captured frame + 2 MB cover.
        used.ShouldBe(40 * OneMegabyte + 40_000 + 2 * OneMegabyte);
    }

    [Fact]
    public async Task A_cover_that_would_pass_the_cap_is_refused()
    {
        storage.StorageCapBytes = 41 * OneMegabyte;
        var row = await ReadyAsync();

        (await admin.CoverUploadAsync(row.Id, new CoverRequest("image/jpeg", 2 * OneMegabyte), Ct))
            .Error!.Code.ShouldBe("VIDEO_STORAGE_FULL");
    }

    [Fact]
    public async Task A_client_can_neither_set_nor_remove_a_cover_and_cannot_see_an_unpublished_one()
    {
        var row = await ReadyAsync();
        await AttachCoverAsync(row.Id);

        caller.UserId = Roles.Client;
        (await admin.CoverUploadAsync(row.Id, new CoverRequest("image/jpeg", 100_000), Ct)).Error!.Code.ShouldBe("FORBIDDEN");
        (await admin.CoverConfirmAsync(row.Id, new CoverConfirm("videos/x-cover-y.jpg"), Ct)).Error!.Code.ShouldBe("FORBIDDEN");
        (await admin.CoverRemoveAsync(row.Id, Ct)).Error!.Code.ShouldBe("FORBIDDEN");

        // The video is Ready but unpublished, so the client cannot see it or its cover at all.
        (await query.DetailAsync(row.Id, false, Ct)).Error!.Code.ShouldBe("VIDEO_NOT_FOUND");
        (await query.ListAsync(new(), false, Ct)).Value.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_published_video_shows_its_cover_to_a_client()
    {
        var row = await ReadyAsync();
        var cover = await AttachCoverAsync(row.Id);
        var current = await db.Videos.AsNoTracking().SingleAsync(v => v.Id == row.Id);
        (await admin.PublishAsync(row.Id, new(current.Revision), true, Ct)).IsSuccess.ShouldBeTrue();

        caller.UserId = Roles.Client;
        var listed = (await query.ListAsync(new(), false, Ct)).Value.Items.Single();
        listed.ThumbnailUrl.ShouldNotBeNull().ShouldContain(cover);
    }

    private sealed class Caller : ICurrentUser
    {
        public string? UserId { get; set; } = Roles.Admin;

        public string? UserAgent => null;

        public string? IpAddress => null;
    }
}
