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
}

public sealed record PasswordInvitationPayload(string To, string FirstName, string Token, DateTime ExpiresAtUtc);

public sealed record PasswordResetPayload(string To, string FirstName, string Token, DateTime ExpiresAtUtc);

public sealed record AdminNewRegistrationPayload(string To, string FirstName, string LastName, string Email);

public sealed record AccountApprovedPayload(string To, string FirstName);

public sealed record AccountDeclinedPayload(string To, string FirstName, string? Reason);

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
