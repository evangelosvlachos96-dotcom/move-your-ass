using System.Globalization;
using Microsoft.Extensions.Configuration;
using Mya.Application.Abstractions.Notifications;
using Mya.Application.Common.Notifications;
using Mya.Domain.Entities;

namespace Mya.Infrastructure.Notifications;

/// <summary>
/// Renders an outbox row into a branded email. Greek copy, hardcoded like the UI. Links use the
/// configured public origin, never an incoming request header, so a host-header injection cannot
/// redirect a password link. Each template describes itself once as <see cref="EmailContent"/>;
/// <see cref="EmailLayout"/> produces the HTML and the plain-text bodies from that.
/// </summary>
public sealed class EmailTemplates(IConfiguration configuration)
{
    private readonly string? _spaOrigin = configuration["App:PublicOrigin"] is { Length: > 0 } origin
        ? origin.TrimEnd('/')
        : null;

    public EmailMessage Render(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.Type switch
        {
            OutboxMessageTypes.PasswordInvitation => PasswordInvitation(
                EmailOutbox.Deserialize<PasswordInvitationPayload>(message.PayloadJson)),
            OutboxMessageTypes.AdminNewRegistration => AdminNewRegistration(
                EmailOutbox.Deserialize<AdminNewRegistrationPayload>(message.PayloadJson)),
            OutboxMessageTypes.AccountApproved => AccountApproved(
                EmailOutbox.Deserialize<AccountApprovedPayload>(message.PayloadJson)),
            OutboxMessageTypes.AccountDeclined => AccountDeclined(
                EmailOutbox.Deserialize<AccountDeclinedPayload>(message.PayloadJson)),
            OutboxMessageTypes.PasswordReset => PasswordReset(
                EmailOutbox.Deserialize<PasswordResetPayload>(message.PayloadJson)),
            OutboxMessageTypes.ContactMessage => ContactMessage(
                EmailOutbox.Deserialize<ContactMessagePayload>(message.PayloadJson)),
            OutboxMessageTypes.TestEmail => TestEmail(
                EmailOutbox.Deserialize<TestEmailPayload>(message.PayloadJson)),
            _ => throw new InvalidOperationException($"No email template for outbox message type '{message.Type}'."),
        };
    }

    private EmailMessage PasswordInvitation(PasswordInvitationPayload p)
    {
        var origin = RequireOrigin();

        // Fragment keeps the credential out of web-server request URLs and referrer headers.
        var link = $"{origin}/set-password#token={Uri.EscapeDataString(p.Token)}";

        return Build(p.To, "Ορίστε τον κωδικό σας στο Move Your Ass", new EmailContent(
            $"Γεια σας {p.FirstName},",
            [
                "Ο λογαριασμός σας στο Move Your Ass είναι έτοιμος. Ορίστε τον κωδικό σας για να τον ενεργοποιήσετε.",
            ])
        {
            Cta = new EmailCta("Ορισμός κωδικού", link),
            Note = $"Ο σύνδεσμος χρησιμοποιείται μία φορά και λήγει στις "
                 + $"{p.ExpiresAtUtc.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} UTC. "
                 + "Αν έχει λήξει, ζητήστε νέα πρόσκληση από τη διαχειρίστρια.",
        });
    }

    private EmailMessage PasswordReset(PasswordResetPayload p)
    {
        var origin = RequireOrigin();
        var link = $"{origin}/reset-password#token={Uri.EscapeDataString(p.Token)}";

        return Build(p.To, "Επαναφορά κωδικού στο Move Your Ass", new EmailContent(
            $"Γεια σας {p.FirstName},",
            [
                "Ζητήθηκε επαναφορά του κωδικού σας. Πατήστε το κουμπί για να ορίσετε νέο κωδικό.",
                "Αν δεν το ζητήσατε εσείς, αγνοήστε αυτό το μήνυμα: ο κωδικός σας παραμένει ο ίδιος.",
            ])
        {
            Cta = new EmailCta("Νέος κωδικός", link),
            Note = $"Ο σύνδεσμος χρησιμοποιείται μία φορά και λήγει στις "
                 + $"{p.ExpiresAtUtc.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} UTC. "
                 + "Μετά την επαναφορά θα χρειαστεί να συνδεθείτε ξανά σε όλες τις συσκευές.",
        });
    }

    /// <summary>
    /// A client's message, forwarded to the trainer. Reply-To is the client, so replying in the
    /// mail client reaches them without anyone copying an address out of the body — and the body
    /// is escaped like every other template, because all of it is user-written.
    /// </summary>
    private EmailMessage ContactMessage(ContactMessagePayload p)
    {
        var built = Build(p.To, $"Μήνυμα από {p.SenderName}: {p.Subject}", new EmailContent(
            $"Νέο μήνυμα από {p.SenderName}",
            [
                $"Από: {p.SenderName} ({p.SenderEmail})",
                $"Θέμα: {p.Subject}",
                p.Message,
            ])
        {
            Note = "Απάντησε απευθείας σε αυτό το email και η απάντηση θα φτάσει στον αποστολέα.",
        });

        return built with { ReplyTo = p.ReplyTo };
    }

    /// <summary>Proves delivery end to end, on demand, without inventing a fake account event.</summary>
    private EmailMessage TestEmail(TestEmailPayload p) =>
        Build(p.To, "Δοκιμαστικό email από το Move Your Ass", new EmailContent(
            $"Γεια σας {p.FirstName},",
            [
                "Αυτό είναι ένα δοκιμαστικό μήνυμα. Αν το διαβάζετε, η αποστολή email λειτουργεί.",
                "Δεν χρειάζεται καμία ενέργεια.",
            ])
        {
            Cta = Link("/dashboard", "Άνοιγμα πίνακα"),
        });

    private EmailMessage AdminNewRegistration(AdminNewRegistrationPayload p) =>
        Build(p.To, $"Νέα εγγραφή: {p.FirstName} {p.LastName}", new EmailContent(
            "Νέα εγγραφή στην πλατφόρμα",
            [
                $"Όνομα: {p.FirstName} {p.LastName}",
                $"Email: {p.Email}",
                "Η εγγραφή περιμένει την έγκρισή σας. Μέχρι τότε δεν είναι δυνατή η σύνδεση.",
            ])
        {
            Cta = Link("/admin/users?status=PendingApproval", "Έλεγχος εγγραφών"),
        });

    private EmailMessage AccountApproved(AccountApprovedPayload p) =>
        Build(p.To, "Ο λογαριασμός σας εγκρίθηκε", new EmailContent(
            $"Γεια σας {p.FirstName},",
            [
                "Ο λογαριασμός σας εγκρίθηκε. Μπορείτε πλέον να συνδεθείτε και να δείτε τις προπονήσεις.",
            ])
        {
            Cta = Link("/login", "Σύνδεση"),
        });

    private EmailMessage AccountDeclined(AccountDeclinedPayload p)
    {
        List<string> paragraphs = ["Δυστυχώς η εγγραφή σας δεν έγινε δεκτή."];
        if (p.Reason is { Length: > 0 })
        {
            paragraphs.Add($"Αιτία: {p.Reason}");
        }

        return Build(p.To, "Η εγγραφή σας δεν έγινε δεκτή",
            new EmailContent($"Γεια σας {p.FirstName},", paragraphs));
    }

    private EmailMessage Build(string to, string subject, EmailContent content)
    {
        var origin = RequireOrigin();
        return new EmailMessage(to, subject, EmailLayout.PlainText(content, origin), EmailLayout.Html(content, origin));
    }

    private EmailCta? Link(string path, string label) =>
        _spaOrigin is null ? null : new EmailCta(label, _spaOrigin + path);

    private string RequireOrigin() =>
        _spaOrigin ?? throw new InvalidOperationException(
            "App:PublicOrigin is required to render emails: every link and the logo are built from it.");
}
