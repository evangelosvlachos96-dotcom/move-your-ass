using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Mya.Application.Common.Notifications;
using Mya.Domain.Entities;
using Mya.Infrastructure.Notifications;
using Shouldly;

namespace Mya.Api.IntegrationTests.Notifications;

/// <summary>
/// Every template has to render both bodies, put user-controlled text through HTML encoding, and
/// build links from the configured origin rather than anything a request supplied.
/// </summary>
public sealed class EmailTemplateTests
{
    private const string Origin = "https://moveyourass.gr";

    private static EmailTemplates Templates(string? origin = Origin) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(origin is null ? [] : new Dictionary<string, string?> { ["App:PublicOrigin"] = origin })
            .Build());

    private static OutboxMessage Message(string type, object payload) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        PayloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        CreatedAtUtc = DateTime.UtcNow,
    };

    public static TheoryData<string, object> AllTemplates() => new()
    {
        { OutboxMessageTypes.PasswordInvitation, new PasswordInvitationPayload("a@example.test", "Μαρία", "tok-123", new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc)) },
        { OutboxMessageTypes.AdminNewRegistration, new AdminNewRegistrationPayload("admin@example.test", "Γιώργος", "Παπαδόπουλος", "g@example.test") },
        { OutboxMessageTypes.AccountApproved, new AccountApprovedPayload("a@example.test", "Μαρία") },
        { OutboxMessageTypes.AccountDeclined, new AccountDeclinedPayload("a@example.test", "Μαρία", "Διπλή εγγραφή") },
        { OutboxMessageTypes.ContactMessage, new ContactMessagePayload("coach@example.test", "m@example.test", "Μαρία Παπά", "m@example.test", "Ερώτηση", "Το μήνυμά μου.") },
    };

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Every_template_renders_a_branded_html_body_and_a_matching_text_body(string type, object payload)
    {
        var message = Templates().Render(Message(type, payload));

        message.Subject.ShouldNotBeNullOrWhiteSpace();
        message.To.ShouldNotBeNullOrWhiteSpace();

        message.Html.ShouldStartWith("<!DOCTYPE html>");
        message.Html.ShouldContain("</html>");
        message.Html.ShouldContain("<table");
        // The logo has to be a PNG served from the site: Gmail refuses to render SVG.
        message.Html.ShouldContain($"{Origin}/email-logo.png");
        message.Html.ShouldNotContain(".svg");
        // Brand colours, inline, because clients strip <style> blocks. The accent only appears
        // where there is a call to action, so it is asserted per-template instead.
        message.Html.ShouldContain("#0e0e10");
        message.Html.ShouldContain("#17171a");
        message.Html.ShouldContain("#f5f5f0");
        message.Html.ShouldContain("Move Your Ass");

        // "powered by Tasos" is real text in the header, not baked into the PNG, so it is crisp
        // and still there when the client blocks images.
        message.Html.ShouldContain("powered by Tasos");
        message.Html.ShouldContain("#ff8a3d");

        message.Text.ShouldNotBeNullOrWhiteSpace();
        message.Text.ShouldNotContain("<");
        message.Text.ShouldContain("Move Your Ass");
        // The plain-text version says the same thing rather than quietly dropping the credit.
        message.Text.ShouldContain("MoveYourAss · powered by Tasos");
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void The_header_still_reads_as_the_brand_with_images_blocked(string type, object payload)
    {
        var message = Templates().Render(Message(type, payload));

        // Everything a client shows when it refuses to load the PNG: the alt text and the credit
        // beneath it, both as text in the document.
        message.Html.ShouldContain("alt=\"Move Your Ass\"");
        message.Html.ShouldContain(">powered by Tasos<");
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Every_template_needs_a_public_origin_rather_than_silently_dropping_links(string type, object payload) =>
        Should.Throw<InvalidOperationException>(() => Templates(origin: null).Render(Message(type, payload)));

    [Fact]
    public void User_supplied_names_are_html_encoded_in_the_html_body()
    {
        var attack = "<script>alert('x')</script>";
        var message = Templates().Render(Message(OutboxMessageTypes.AdminNewRegistration,
            new AdminNewRegistrationPayload("admin@example.test", attack, "Παπαδόπουλος", "g@example.test")));

        message.Html.ShouldNotContain("<script>");
        message.Html.ShouldContain("&lt;script&gt;");
        // The text body is not markup, so it carries the raw characters and that is correct.
        message.Text.ShouldContain(attack);
    }

    [Fact]
    public void A_decline_reason_is_encoded_and_omitted_when_absent()
    {
        var withReason = Templates().Render(Message(OutboxMessageTypes.AccountDeclined,
            new AccountDeclinedPayload("a@example.test", "Μαρία", "<b>duplicate</b>")));
        withReason.Html.ShouldContain("&lt;b&gt;duplicate&lt;/b&gt;");
        withReason.Html.ShouldNotContain("<b>duplicate</b>");

        var without = Templates().Render(Message(OutboxMessageTypes.AccountDeclined,
            new AccountDeclinedPayload("a@example.test", "Μαρία", null)));
        without.Html.ShouldNotContain("Αιτία");
        without.Text.ShouldNotContain("Αιτία");
    }

    [Fact]
    public void The_invitation_carries_a_single_use_link_as_a_fragment_in_both_bodies()
    {
        var message = Templates().Render(Message(OutboxMessageTypes.PasswordInvitation,
            new PasswordInvitationPayload("a@example.test", "Μαρία", "tok+123/abc", new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc))));

        var expected = $"{Origin}/set-password#token=tok%2B123%2Fabc";
        message.Text.ShouldContain(expected);
        // Encoded in HTML because the raw token can contain & and other markup-significant bytes.
        message.Html.ShouldContain("/set-password#token=tok%2B123%2Fabc");
        message.Html.ShouldContain("Ορισμός κωδικού");
        message.Text.ShouldContain("01/10/2026 09:30");
    }

    [Fact]
    public void A_call_to_action_appears_as_a_button_and_as_a_bare_url_for_clients_that_strip_links()
    {
        var message = Templates().Render(Message(OutboxMessageTypes.AccountApproved,
            new AccountApprovedPayload("a@example.test", "Μαρία")));

        message.Html.ShouldContain($"href=\"{Origin}/login\"");
        message.Html.ShouldContain("Σύνδεση");
        // The button carries the lime accent with dark text on it; white on lime is unreadable.
        message.Html.ShouldContain("#c6f24e");
        // Repeated in plain sight, not only inside the anchor.
        message.Html.ShouldContain($">{Origin}/login</p>");
        message.Text.ShouldContain($"{Origin}/login");
    }

    [Fact]
    public void The_contact_message_replies_to_the_client_and_escapes_what_they_wrote()
    {
        var message = Templates().Render(Message(OutboxMessageTypes.ContactMessage,
            new ContactMessagePayload("coach@example.test", "m@example.test", "Μαρία", "m@example.test",
                "<b>Θέμα</b>", "<script>alert(1)</script> γεια")));

        // Replying in the mail client reaches the client, not the no-reply sender.
        message.ReplyTo.ShouldBe("m@example.test");
        message.To.ShouldBe("coach@example.test");
        message.Subject.ShouldContain("Μαρία");

        // Every part of this is written by a stranger on the internet.
        message.Html.ShouldNotContain("<script>");
        message.Html.ShouldContain("&lt;script&gt;");
        message.Html.ShouldContain("&lt;b&gt;Θέμα&lt;/b&gt;");
    }

    [Fact]
    public void Only_the_contact_message_sets_a_reply_to()
    {
        Templates().Render(Message(OutboxMessageTypes.AccountDeclined, new AccountDeclinedPayload("a@example.test", "Μαρία", null)))
            .ReplyTo.ShouldBeNull();
        Templates().Render(Message(OutboxMessageTypes.AccountApproved, new AccountApprovedPayload("a@example.test", "Μαρία")))
            .ReplyTo.ShouldBeNull();
    }

    [Fact]
    public void An_unknown_outbox_type_fails_loudly_rather_than_sending_an_empty_email() =>
        Should.Throw<InvalidOperationException>(() => Templates().Render(Message("NoSuchType", new { })));

    /// <summary>
    /// Writes every template to disk so a human can look at it in a browser. Automated assertions
    /// cannot tell you an email is ugly. Set MYA_EMAIL_PREVIEW_DIR to a folder and run the suite;
    /// without it this does nothing. See docs/09-email-setup.md.
    /// </summary>
    [Fact]
    public void Preview_every_template_to_disk_when_asked()
    {
        var directory = Environment.GetEnvironmentVariable("MYA_EMAIL_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        foreach (var row in AllTemplates())
        {
            var type = (string)row[0]!;
            var rendered = Templates().Render(Message(type, row[1]!));
            File.WriteAllText(Path.Combine(directory, $"{type}.html"), rendered.Html);
            File.WriteAllText(Path.Combine(directory, $"{type}.txt"), rendered.Text);
        }
    }
}
