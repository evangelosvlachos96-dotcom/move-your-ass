using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Notifications;
using Mya.Application.Common.Results;
using Mya.Domain.Constants;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Site;

/// <summary>
/// The About page contact form, and the admin's own delivery test.
///
/// Both queue through the outbox rather than calling Resend inline, so a provider outage delays a
/// message instead of failing the request the user is looking at, and retries are already solved.
/// </summary>
public sealed class ContactHandler(
    IAppDbContext db,
    IUserService users,
    ICurrentUser current,
    IClock clock)
{
    /// <summary>
    /// How long an identical message from the same person is treated as a double submit rather
    /// than a second message. Long enough to cover a double-tapped button and an impatient retry,
    /// short enough that somebody genuinely resending after a few minutes is not blocked.
    /// </summary>
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(10);

    public async Task<Result> SendAsync(ContactInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (current.UserId is not { Length: > 0 } userId) return Result.Failure(SiteRules.Forbidden);
        var sender = await users.FindByIdAsync(userId, ct);
        if (sender is not { Status: UserStatus.Active, MustChangePassword: false }) return Result.Failure(SiteRules.Forbidden);

        var subject = input.Subject.Trim();
        var message = input.Message.Trim();
        if (subject.Length == 0 || message.Length == 0) return Result.Failure(SiteRules.Invalid);

        var recipients = await RecipientsAsync(ct);
        if (recipients.Count == 0) return Result.Failure(SiteRules.NoRecipient);

        var now = clock.UtcNow;
        var fingerprint = Fingerprint(userId, subject, message);
        var since = now - DuplicateWindow;

        // The outbox is the record of what was sent, so it is also where a repeat is visible.
        var alreadySent = await db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Type == OutboxMessageTypes.ContactMessage && m.CreatedAtUtc >= since)
            .AnyAsync(m => m.SubjectUserId == fingerprint, ct);
        if (alreadySent) return Result.Failure(SiteRules.Duplicate);

        // One message per recipient: several addresses in one "to" is not a thing the provider
        // accepts, and one failing address should not hold up the others.
        foreach (var recipient in recipients)
        {
            db.OutboxMessages.Add(EmailOutbox.ContactMessage(recipient, sender, subject, message, now, fingerprint));
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Queues a branded message to the signed-in admin, so delivery can be proved in production
    /// at any time without waiting for a real account event to happen.
    /// </summary>
    public async Task<Result> SendTestAsync(CancellationToken ct)
    {
        if (current.UserId is not { Length: > 0 } userId) return Result.Failure(SiteRules.Forbidden);
        var admin = await users.FindByIdAsync(userId, ct);
        if (admin is not { Status: UserStatus.Active } || admin.Role != Roles.Admin) return Result.Failure(SiteRules.Forbidden);

        db.OutboxMessages.Add(EmailOutbox.TestEmail(admin, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// The trainer's published contact address, or every admin if none is set. A message with
    /// nowhere to go is refused rather than silently dropped.
    /// </summary>
    private async Task<IReadOnlyList<string>> RecipientsAsync(CancellationToken ct)
    {
        var configured = await db.SiteContent.AsNoTracking()
            .Select(x => x.ContactEmail)
            .SingleOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(configured)) return [configured];

        return await users.GetEmailsInRoleAsync(Roles.Admin, ct);
    }

    private static string Fingerprint(string userId, string subject, string message) =>
        "contact:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}\n{subject}\n{message}")))[..48];
}
