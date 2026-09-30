using System.Text.Json;
using Mya.Application.Abstractions.Identity;
using Mya.Domain.Entities;

namespace Mya.Application.Common.Notifications;

public static class OutboxMessageTypes
{
    public const string PasswordInvitation = "PasswordInvitation";
    public const string AdminNewRegistration = "AdminNewRegistration";
    public const string AccountApproved = "AccountApproved";
    public const string AccountDeclined = "AccountDeclined";
    public const string PasswordReset = "PasswordReset";
    public const string ContactMessage = "ContactMessage";
}

public sealed record PasswordInvitationPayload(string To, string FirstName, string Token, DateTime ExpiresAtUtc);

public sealed record PasswordResetPayload(string To, string FirstName, string Token, DateTime ExpiresAtUtc);

public sealed record AdminNewRegistrationPayload(string To, string FirstName, string LastName, string Email);

public sealed record AccountApprovedPayload(string To, string FirstName);

public sealed record AccountDeclinedPayload(string To, string FirstName, string? Reason);

/// <summary>A message a client sent from the About page. ReplyTo is the client's own address.</summary>
public sealed record ContactMessagePayload(string To, string ReplyTo, string SenderName, string SenderEmail, string Subject, string Message);

/// <summary>An admin proving delivery works, to their own address.</summary>

/// <summary>
/// Builds account notification and invitation emails. Handlers add these in the same
/// transaction as the state change; the dispatcher renders and sends them later (ADR-010).
/// </summary>
public static class EmailOutbox
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static OutboxMessage AdminNewRegistration(string adminEmail, UserAccount registered, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(registered);
        return Create(
            OutboxMessageTypes.AdminNewRegistration,
            new AdminNewRegistrationPayload(adminEmail, registered.FirstName, registered.LastName, registered.Email),
            nowUtc,
            // Addressed to the admin, about the registrant. Deleting the registrant should not
            // withdraw the admin's notification, so this one carries no subject.
            subjectUserId: null);
    }

    public static OutboxMessage AccountApproved(UserAccount user, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Create(OutboxMessageTypes.AccountApproved, new AccountApprovedPayload(user.Email, user.FirstName), nowUtc, user.Id);
    }

    public static OutboxMessage AccountDeclined(UserAccount user, string? reason, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Create(OutboxMessageTypes.AccountDeclined, new AccountDeclinedPayload(user.Email, user.FirstName, reason), nowUtc, user.Id);
    }

    public static OutboxMessage PasswordInvitation(UserAccount user, string token, DateTime nowUtc, DateTime expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Create(OutboxMessageTypes.PasswordInvitation,
            new PasswordInvitationPayload(user.Email, user.FirstName, token, expiresAtUtc), nowUtc, user.Id);
    }

    public static OutboxMessage PasswordReset(UserAccount user, string token, DateTime nowUtc, DateTime expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Create(OutboxMessageTypes.PasswordReset,
            new PasswordResetPayload(user.Email, user.FirstName, token, expiresAtUtc), nowUtc, user.Id);
    }

    /// <summary>
    /// One message per recipient. <paramref name="fingerprint"/> rides in the subject column,
    /// which is what lets a repeat submission be spotted with one indexed read rather than a scan
    /// of message bodies. It is deliberately not a user id: deleting the client must not withdraw
    /// a message the trainer may already be acting on.
    /// </summary>
    public static OutboxMessage ContactMessage(string to, UserAccount sender, string subject, string message, DateTime nowUtc, string fingerprint)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return Create(OutboxMessageTypes.ContactMessage,
            new ContactMessagePayload(to, sender.Email, $"{sender.FirstName} {sender.LastName}".Trim(), sender.Email, subject, message),
            nowUtc,
            subjectUserId: fingerprint);
    }

    public static T Deserialize<T>(string payloadJson) =>
        JsonSerializer.Deserialize<T>(payloadJson, Json)
        ?? throw new InvalidOperationException($"Outbox payload could not be read as {typeof(T).Name}.");

    private static OutboxMessage Create<T>(string type, T payload, DateTime nowUtc, string? subjectUserId) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        PayloadJson = JsonSerializer.Serialize(payload, Json),
        CreatedAtUtc = nowUtc,
        SubjectUserId = subjectUserId,
    };
}
