using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mya.Api.IntegrationTests.Videos;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Notifications;
using Mya.Application.Common.Time;
using Mya.Application.Features.Site;
using Mya.Application.Features.Videos;
using Mya.Domain.Constants;
using Mya.Domain.Entities;
using Mya.Domain.Enums;
using Mya.Infrastructure;
using Mya.Infrastructure.Identity;
using Mya.Infrastructure.Persistence;
using Shouldly;

namespace Mya.Api.IntegrationTests.Site;

/// <summary>
/// The About page: who may read it, who may write it, what a link is allowed to be, and what the
/// contact form actually queues.
/// </summary>
public sealed class SiteContentTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private ServiceProvider provider = null!;
    private IServiceScope scope = null!;
    private AppDbContext db = null!;
    private readonly Caller caller = new();
    private readonly FakeVideoStorage storage = new();
    private SiteContentHandler site = null!;
    private ContactHandler contact = null!;
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
                Status = UserStatus.Active, FirstName = role == Roles.Admin ? "Τάσος" : "Μαρία", LastName = "Δοκιμή",
            };
            (await users.CreateAsync(user)).Succeeded.ShouldBeTrue();
            (await users.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
        }

        var clock = new UtcClock();
        var service = new UserService(users, db, clock);
        var access = new VideoAccess(service, caller);
        site = new SiteContentHandler(db, access, clock, storage);
        contact = new ContactHandler(db, service, caller, clock);
    }

    public async Task DisposeAsync()
    {
        scope.Dispose();
        await provider.DisposeAsync();
        await connection.DisposeAsync();
    }

    private static readonly SocialLink[] Links =
    [
        new("instagram", "https://instagram.com/coach"),
        new("whatsapp", "306912345678"),
        new("website", "https://example.test"),
    ];

    private static AboutInput Input(Guid revision) => new(
        "Τάσος Παπαδόπουλος", "Προπονητής", "Γεια σου!", "coach@example.test", "+30 210 1234567",
        "https://booking.example.test/coach", Links, revision);

    private async Task<Guid> SeedAsync()
    {
        caller.UserId = Roles.Admin;
        var current = (await site.GetAsync(Ct)).Value.Revision;
        (await site.UpdateAsync(Input(current), Ct)).IsSuccess.ShouldBeTrue();
        return (await site.GetAsync(Ct)).Value.Revision;
    }

    // --- access ---------------------------------------------------------------------------

    [Fact]
    public async Task A_client_can_read_the_page_but_not_change_it()
    {
        await SeedAsync();
        caller.UserId = Roles.Client;

        var read = await site.GetAsync(Ct);
        read.IsSuccess.ShouldBeTrue();
        read.Value.TrainerName.ShouldBe("Τάσος Παπαδόπουλος");

        (await site.UpdateAsync(Input(read.Value.Revision), Ct)).Error!.Code.ShouldBe("FORBIDDEN");
        (await site.PhotoRemoveAsync(Ct)).Error!.Code.ShouldBe("FORBIDDEN");
    }

    [Fact]
    public async Task Nobody_signed_in_sees_anything()
    {
        await SeedAsync();
        caller.UserId = null;

        (await site.GetAsync(Ct)).Error!.Code.ShouldBe("FORBIDDEN");
        (await site.UpdateAsync(Input(Guid.NewGuid()), Ct)).Error!.Code.ShouldBe("FORBIDDEN");
    }

    [Fact]
    public async Task Reading_an_untouched_page_returns_empties_rather_than_creating_a_row()
    {
        caller.UserId = Roles.Client;

        var read = await site.GetAsync(Ct);
        read.IsSuccess.ShouldBeTrue();
        read.Value.TrainerName.ShouldBeNull();
        read.Value.Revision.ShouldBe(Guid.Empty);
        // A client opening the page must not write to the database.
        (await db.SiteContent.CountAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task A_stale_revision_cannot_overwrite_a_newer_save()
    {
        var first = await SeedAsync();
        caller.UserId = Roles.Admin;

        (await site.UpdateAsync(Input(first) with { Tagline = "Νέο" }, Ct)).IsSuccess.ShouldBeTrue();
        (await site.UpdateAsync(Input(first) with { Tagline = "Παλιό" }, Ct)).Error!.Code.ShouldBe("SITE_CONFLICT");
        (await site.GetAsync(Ct)).Value.Tagline.ShouldBe("Νέο");
    }

    // --- validation and sanitisation --------------------------------------------------------

    [Theory]
    [InlineData("http://example.test")]
    [InlineData("javascript:alert(1)")]
    [InlineData("example.test")]
    [InlineData("/relative")]
    [InlineData("ftp://example.test")]
    public void Only_absolute_https_links_are_allowed(string url) => SiteRules.IsSafeUrl(url).ShouldBeFalse();

    [Fact]
    public void An_https_link_is_allowed() => SiteRules.IsSafeUrl("https://instagram.com/coach").ShouldBeTrue();

    [Fact]
    public void The_validator_refuses_a_non_https_link_and_a_bad_phone()
    {
        var validator = new AboutInputValidator();
        var ok = Input(Guid.Empty);

        validator.Validate(ok).IsValid.ShouldBeTrue();
        validator.Validate(ok with { ContactEmail = "not-an-email" }).IsValid.ShouldBeFalse();
        validator.Validate(ok with { Phone = "<script>" }).IsValid.ShouldBeFalse();

        // Every link goes into an href, so a scheme other than https is refused rather than
        // upgraded, and a network nobody defined is refused outright.
        Refuse(validator, ok, new SocialLink("instagram", "javascript:alert(1)"));
        Refuse(validator, ok, new SocialLink("website", "http://example.test"));
        Refuse(validator, ok, new SocialLink("myspace", "https://example.test"));
        // WhatsApp becomes a wa.me path, so it is digits or nothing.
        Refuse(validator, ok, new SocialLink("whatsapp", "+30 691"));
        Refuse(validator, ok, new SocialLink("whatsapp", "https://example.test"));
    }

    private static void Refuse(AboutInputValidator validator, AboutInput ok, SocialLink link) =>
        validator.Validate(ok with { SocialLinks = [link] }).IsValid.ShouldBeFalse(
            $"{link.Network} = {link.Value} should have been refused");

    [Fact]
    public void The_validator_refuses_a_booking_link_that_is_not_https()
    {
        var validator = new AboutInputValidator();
        var ok = Input(Guid.Empty);

        validator.Validate(ok with { BookingUrl = "http://booking.example.test" }).IsValid.ShouldBeFalse();
        validator.Validate(ok with { BookingUrl = "javascript:alert(1)" }).IsValid.ShouldBeFalse();
        validator.Validate(ok with { BookingUrl = "calendly.com/coach" }).IsValid.ShouldBeFalse();
        // Empty is the normal state: no link, no buttons anywhere.
        validator.Validate(ok with { BookingUrl = null }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task The_real_booking_link_survives_validation_and_storage_unchanged()
    {
        // The trainer's actual scheduling page. The double underscore in the path is the reason
        // this is pinned: URI parsing normalises plenty of things, and a link that comes back
        // one character different is a link that 404s.
        const string url = "https://reply-now.com/book/tasos__ch";

        SiteRules.IsSafeUrl(url).ShouldBeTrue();
        new AboutInputValidator().Validate(Input(Guid.Empty) with { BookingUrl = url }).IsValid.ShouldBeTrue();

        caller.UserId = Roles.Admin;
        var revision = (await site.GetAsync(Ct)).Value.Revision;
        (await site.UpdateAsync(Input(revision) with { BookingUrl = url }, Ct)).IsSuccess.ShouldBeTrue();

        (await site.GetAsync(Ct)).Value.BookingUrl.ShouldBe(url);
    }

    [Fact]
    public async Task A_booking_link_that_no_longer_passes_the_rules_is_dropped_on_the_way_out()
    {
        caller.UserId = Roles.Admin;
        await SeedAsync();

        var row = await db.SiteContent.SingleAsync(Ct);
        row.BookingUrl = "http://insecure.example";
        await db.SaveChangesAsync(Ct);

        (await site.GetAsync(Ct)).Value.BookingUrl.ShouldBeNull();
    }

    [Fact]
    public void The_validator_refuses_the_same_network_twice()
    {
        var validator = new AboutInputValidator();
        var twice = Input(Guid.Empty) with
        {
            SocialLinks =
            [
                new("instagram", "https://instagram.com/one"),
                new("instagram", "https://instagram.com/two"),
            ],
        };

        validator.Validate(twice).IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Only_the_networks_that_were_added_come_back_and_in_the_order_they_were_added()
    {
        caller.UserId = Roles.Admin;
        var revision = (await site.GetAsync(Ct)).Value.Revision;

        (await site.UpdateAsync(Input(revision), Ct)).IsSuccess.ShouldBeTrue();

        var read = (await site.GetAsync(Ct)).Value.SocialLinks;
        read.Select(l => l.Network).ShouldBe(["instagram", "whatsapp", "website"]);
        read[0].Value.ShouldBe("https://instagram.com/coach");
        // The five networks that were not added are simply absent, not empty.
        read.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Removing_every_network_leaves_none_rather_than_empty_rows()
    {
        caller.UserId = Roles.Admin;
        var revision = await SeedAsync();

        (await site.UpdateAsync(Input(revision) with { SocialLinks = [] }, Ct)).IsSuccess.ShouldBeTrue();

        (await site.GetAsync(Ct)).Value.SocialLinks.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_link_that_no_longer_passes_the_rules_is_dropped_on_the_way_out()
    {
        caller.UserId = Roles.Admin;
        await SeedAsync();

        // Straight into the column, the way a migration or an older version of the rules could
        // have left it. Stored text outlives the code that wrote it.
        var row = await db.SiteContent.SingleAsync(Ct);
        row.SocialLinksJson =
            "[{\"network\":\"instagram\",\"value\":\"http://insecure.example\"},"
            + "{\"network\":\"website\",\"value\":\"https://good.example\"}]";
        await db.SaveChangesAsync(Ct);

        var read = (await site.GetAsync(Ct)).Value.SocialLinks;
        read.Select(l => l.Network).ShouldBe(["website"]);
    }

    [Fact]
    public async Task Nonsense_in_the_column_reads_as_no_links_rather_than_throwing()
    {
        caller.UserId = Roles.Admin;
        await SeedAsync();

        var row = await db.SiteContent.SingleAsync(Ct);
        row.SocialLinksJson = "not json at all";
        await db.SaveChangesAsync(Ct);

        (await site.GetAsync(Ct)).Value.SocialLinks.ShouldBeEmpty();
    }

    [Fact]
    public void Markdown_is_stripped_of_anything_that_could_become_markup()
    {
        SiteRules.SanitizeMarkdown("<script>alert(1)</script>Γεια").ShouldBe("alert(1)Γεια");
        // Nothing but a tag leaves nothing behind, which is stored as null, not "".
        SiteRules.SanitizeMarkdown("<img src=x onerror=y>").ShouldBeNull();
        SiteRules.SanitizeMarkdown("**bold** and - a list").ShouldBe("**bold** and - a list");
        SiteRules.SanitizeMarkdown("   ").ShouldBeNull();
        SiteRules.SanitizeMarkdown("a\r\nb").ShouldBe("a\nb");
    }

    [Fact]
    public async Task Stored_markdown_has_already_been_sanitised()
    {
        caller.UserId = Roles.Admin;
        var revision = (await site.GetAsync(Ct)).Value.Revision;

        (await site.UpdateAsync(Input(revision) with { AboutMarkdown = "<b>Γεια</b> <script>x</script>" }, Ct))
            .IsSuccess.ShouldBeTrue();

        var stored = (await site.GetAsync(Ct)).Value.AboutMarkdown;
        stored.ShouldNotBeNull();
        stored.ShouldNotContain("<");
        stored.ShouldContain("Γεια");
    }

    [Fact]
    public async Task An_empty_field_is_stored_as_nothing_rather_than_as_whitespace()
    {
        caller.UserId = Roles.Admin;
        var revision = (await site.GetAsync(Ct)).Value.Revision;

        (await site.UpdateAsync(
            Input(revision) with { Tagline = "   ", SocialLinks = [new SocialLink("website", "  ")] },
            Ct)).IsSuccess.ShouldBeTrue();

        var read = (await site.GetAsync(Ct)).Value;
        read.Tagline.ShouldBeNull();
        read.SocialLinks.ShouldBeEmpty();
    }

    // --- photo -----------------------------------------------------------------------------

    [Fact]
    public async Task A_photo_is_type_and_size_checked_and_replacing_one_deletes_the_old()
    {
        caller.UserId = Roles.Admin;

        (await site.PhotoUploadAsync(new CoverRequest("image/gif", 1000), Ct)).Error!.Code.ShouldBe("VIDEO_COVER_TYPE");
        (await site.PhotoUploadAsync(new CoverRequest("image/jpeg", 6 * 1024 * 1024), Ct)).Error!.Code.ShouldBe("VIDEO_COVER_TOO_LARGE");

        var first = (await site.PhotoUploadAsync(new CoverRequest("image/jpeg", 50_000), Ct)).Value;
        storage.Seed(first.ObjectKey, 50_000, "image/jpeg");
        (await site.PhotoConfirmAsync(new PhotoConfirm(first.ObjectKey), Ct)).IsSuccess.ShouldBeTrue();
        (await site.GetAsync(Ct)).Value.PhotoUrl.ShouldNotBeNull().ShouldContain(first.ObjectKey);

        var second = (await site.PhotoUploadAsync(new CoverRequest("image/webp", 40_000), Ct)).Value;
        storage.Seed(second.ObjectKey, 40_000, "image/webp");
        (await site.PhotoConfirmAsync(new PhotoConfirm(second.ObjectKey), Ct)).IsSuccess.ShouldBeTrue();

        storage.Objects.ShouldNotContainKey(first.ObjectKey);
        storage.Objects.ShouldContainKey(second.ObjectKey);

        (await site.PhotoRemoveAsync(Ct)).IsSuccess.ShouldBeTrue();
        storage.Objects.ShouldNotContainKey(second.ObjectKey);
        (await site.GetAsync(Ct)).Value.PhotoUrl.ShouldBeNull();
    }

    [Fact]
    public async Task A_photo_key_from_outside_the_site_folder_is_refused()
    {
        caller.UserId = Roles.Admin;
        storage.Seed("videos/someone-else.jpg", 1000, "image/jpeg");

        (await site.PhotoConfirmAsync(new PhotoConfirm("videos/someone-else.jpg"), Ct))
            .Error!.Code.ShouldBe("SITE_INVALID");
    }

    // --- contact form ------------------------------------------------------------------------

    [Fact]
    public async Task A_client_message_is_queued_to_the_trainer_with_reply_to_set()
    {
        await SeedAsync();
        caller.UserId = Roles.Client;

        (await contact.SendAsync(new ContactInput("Ερώτηση", "Θέλω να ρωτήσω κάτι."), Ct)).IsSuccess.ShouldBeTrue();

        var queued = await db.OutboxMessages.SingleAsync(m => m.Type == OutboxMessageTypes.ContactMessage, Ct);
        var payload = EmailOutbox.Deserialize<ContactMessagePayload>(queued.PayloadJson);
        payload.To.ShouldBe("coach@example.test");
        payload.ReplyTo.ShouldBe("Client@example.test");
        payload.SenderName.ShouldBe("Μαρία Δοκιμή");
        payload.Subject.ShouldBe("Ερώτηση");
    }

    [Fact]
    public async Task With_no_contact_address_the_message_goes_to_every_admin()
    {
        caller.UserId = Roles.Client;

        (await contact.SendAsync(new ContactInput("Ερώτηση", "Θέλω να ρωτήσω κάτι."), Ct)).IsSuccess.ShouldBeTrue();

        var queued = await db.OutboxMessages.Where(m => m.Type == OutboxMessageTypes.ContactMessage).ToListAsync(Ct);
        queued.Count.ShouldBe(1);
        EmailOutbox.Deserialize<ContactMessagePayload>(queued[0].PayloadJson).To.ShouldBe("Admin@example.test");
    }

    [Fact]
    public async Task The_same_message_twice_is_treated_as_a_double_submit()
    {
        await SeedAsync();
        caller.UserId = Roles.Client;
        var message = new ContactInput("Ερώτηση", "Θέλω να ρωτήσω κάτι.");

        (await contact.SendAsync(message, Ct)).IsSuccess.ShouldBeTrue();
        (await contact.SendAsync(message, Ct)).Error!.Code.ShouldBe("CONTACT_DUPLICATE");

        // A genuinely different message still goes through.
        (await contact.SendAsync(new ContactInput("Άλλο", "Κάτι εντελώς διαφορετικό."), Ct)).IsSuccess.ShouldBeTrue();
        (await db.OutboxMessages.CountAsync(m => m.Type == OutboxMessageTypes.ContactMessage, Ct)).ShouldBe(2);
    }

    [Fact]
    public void The_validator_refuses_empty_and_too_short_messages()
    {
        var validator = new ContactInputValidator();
        validator.Validate(new ContactInput("", "")).IsValid.ShouldBeFalse();
        validator.Validate(new ContactInput("Hi", "short")).IsValid.ShouldBeFalse();
        validator.Validate(new ContactInput("Ερώτηση", new string('a', 4001))).IsValid.ShouldBeFalse();
        validator.Validate(new ContactInput("Ερώτηση", "Αρκετά μεγάλο μήνυμα.")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Somebody_not_signed_in_cannot_send()
    {
        caller.UserId = null;
        (await contact.SendAsync(new ContactInput("Ερώτηση", "Θέλω να ρωτήσω κάτι."), Ct)).Error!.Code.ShouldBe("FORBIDDEN");
    }

    [Fact]
    public async Task An_admin_cannot_use_the_contact_form()
    {
        await SeedAsync();
        caller.UserId = Roles.Admin;

        // The form writes to the trainer, so an admin using it is writing to themselves. The UI
        // hides it; this is the half that matters, because the endpoint is the control.
        (await contact.SendAsync(new ContactInput("Ερώτηση", "Θέλω να ρωτήσω κάτι."), Ct))
            .Error!.Code.ShouldBe("FORBIDDEN");
        (await db.OutboxMessages.CountAsync(m => m.Type == OutboxMessageTypes.ContactMessage, Ct)).ShouldBe(0);
    }

    private sealed class Caller : ICurrentUser
    {
        public string? UserId { get; set; } = Roles.Admin;

        public string? UserAgent => null;

        public string? IpAddress => null;
    }
}
