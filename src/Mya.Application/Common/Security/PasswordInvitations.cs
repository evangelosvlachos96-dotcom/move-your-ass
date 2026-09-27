using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Notifications;
using Mya.Application.Common.Settings;
using Mya.Domain.Entities;
using Mya.Domain.Enums;

namespace Mya.Application.Common.Security;

/// <summary>Called inside the handler's transaction so the credential and email commit together.</summary>
public sealed class PasswordInvitations(IAppDbContext db, IClock clock, IOptions<PlatformSettings> settings)
{
    public Task QueueAsync(UserAccount user, CancellationToken cancellationToken) =>
        IssueAsync(user, PasswordInvitationPurpose.Invitation, cancellationToken);

    /// <summary>
    /// A forgotten-password link. Shorter-lived than an invitation: an invitation is expected to
    /// sit in an inbox until someone gets round to it, a reset is acted on immediately.
    /// </summary>
    public Task QueueResetAsync(UserAccount user, CancellationToken cancellationToken) =>
        IssueAsync(user, PasswordInvitationPurpose.Reset, cancellationToken);

    private async Task IssueAsync(UserAccount user, PasswordInvitationPurpose purpose, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var now = clock.UtcNow;
        var expires = purpose == PasswordInvitationPurpose.Reset
            ? now.AddHours(1)
            : now.AddHours(settings.Value.InvitationHours);

        // Issuing a new credential invalidates every outstanding one for this user, whatever it
        // was for. Two live password links for one account is one more than anybody needs.
        await db.PasswordInvitations.Where(x => x.UserId == user.Id && x.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAtUtc, now), cancellationToken);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.PasswordInvitations.Add(new PasswordInvitation
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = Hash(token), Purpose = purpose,
            CreatedAtUtc = now, ExpiresAtUtc = expires,
        });
        db.OutboxMessages.Add(purpose == PasswordInvitationPurpose.Reset
            ? EmailOutbox.PasswordReset(user, token, now, expires)
            : EmailOutbox.PasswordInvitation(user, token, now, expires));
        await db.SaveChangesAsync(cancellationToken);
    }

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
