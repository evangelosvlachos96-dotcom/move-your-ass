using Microsoft.Extensions.Configuration;
using Mya.Application.Abstractions.Notifications;
using Mya.Application.Common.Notifications;
using Mya.Domain.Entities;

namespace Mya.Infrastructure.Notifications;

/// <summary>
/// Renders an outbox row into a plain-text email. Greek copy, hardcoded for now like the UI.
/// Links point at the SPA origin, which is the same value CORS allows.
/// </summary>
public sealed class EmailTemplates(IConfiguration configuration)
{
    private readonly string? _spaOrigin = configuration["Cors:AllowedOrigin"] is { Length: > 0 } origin
        ? origin.TrimEnd('/')
        : null;

    public EmailMessage Render(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.Type switch
        {
            OutboxMessageTypes.AdminNewRegistration => AdminNewRegistration(
                EmailOutbox.Deserialize<AdminNewRegistrationPayload>(message.PayloadJson)),
            OutboxMessageTypes.AccountApproved => AccountApproved(
                EmailOutbox.Deserialize<AccountApprovedPayload>(message.PayloadJson)),
            OutboxMessageTypes.AccountDeclined => AccountDeclined(
                EmailOutbox.Deserialize<AccountDeclinedPayload>(message.PayloadJson)),
            _ => throw new InvalidOperationException($"No email template for outbox message type '{message.Type}'."),
        };
    }

    private EmailMessage AdminNewRegistration(AdminNewRegistrationPayload p) => new(
        p.To,
        $"Νέα εγγραφή: {p.FirstName} {p.LastName}",
        $"""
        Νέα εγγραφή στην πλατφόρμα.

        Όνομα:  {p.FirstName} {p.LastName}
        Email:  {p.Email}

        Η εγγραφή περιμένει έγκριση.{Link("/admin/users?status=PendingApproval", "Εκκρεμείς εγγραφές")}
        """);

    private EmailMessage AccountApproved(AccountApprovedPayload p) => new(
        p.To,
        "Ο λογαριασμός σας εγκρίθηκε",
        $"""
        Γεια σας {p.FirstName},

        ο λογαριασμός σας εγκρίθηκε. Μπορείτε πλέον να συνδεθείτε.{Link("/login", "Σύνδεση")}
        """);

    private static EmailMessage AccountDeclined(AccountDeclinedPayload p) => new(
        p.To,
        "Η εγγραφή σας δεν έγινε δεκτή",
        $"""
        Γεια σας {p.FirstName},

        δυστυχώς η εγγραφή σας δεν έγινε δεκτή.{(p.Reason is null ? string.Empty : $"\n\nΑιτία: {p.Reason}")}
        """);

    private string Link(string path, string label) =>
        _spaOrigin is null ? string.Empty : $"\n\n{label}: {_spaOrigin}{path}";
}
