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

namespace Mya.Application.Common.Security;

/// <summary>Called inside the handler's transaction so the credential and email commit together.</summary>
public sealed class PasswordInvitations(IAppDbContext db, IClock clock, IOptions<PlatformSettings> settings)
{
    public async Task QueueAsync(UserAccount user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var now = clock.UtcNow;
        var expires = now.AddHours(settings.Value.InvitationHours);
        await db.PasswordInvitations.Where(x => x.UserId == user.Id && x.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAtUtc, now), cancellationToken);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.PasswordInvitations.Add(new PasswordInvitation
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = Hash(token),
            CreatedAtUtc = now, ExpiresAtUtc = expires,
        });
        db.OutboxMessages.Add(EmailOutbox.PasswordInvitation(user, token, now, expires));
        await db.SaveChangesAsync(cancellationToken);
    }

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
