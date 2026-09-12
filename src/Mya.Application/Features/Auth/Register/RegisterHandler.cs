using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Notifications;
using Mya.Application.Common.Results;
using Mya.Domain.Constants;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Auth.Register;

/// <summary>
/// Self-service path: the account is created pending and every Admin is emailed.
/// A Declined account may register again with the same email: it is reset (new names, new
/// password, back to PendingApproval) and treated exactly like a fresh registration. Any other
/// existing status still ends in EMAIL_ALREADY_EXISTS, decided by the unique index.
/// </summary>
public sealed class RegisterHandler(IUserService users, IAppDbContext db, IClock clock)
{
    public async Task<Result<RegisterResponse>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var email = command.Email.Trim();
        var firstName = command.FirstName.Trim();
        var lastName = command.LastName.Trim();

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        UserAccount registered;

        var existing = await users.FindByEmailAsync(email, cancellationToken);
        if (existing is { Status: UserStatus.Declined })
        {
            registered = await users.ReRegisterDeclinedAsync(existing.Id, firstName, lastName, command.Password, cancellationToken);
        }
        else
        {
            var created = await users.CreateAsync(
                new NewUserAccount(email, firstName, lastName, Roles.Client, UserStatus.PendingApproval, MustChangePassword: false),
                command.Password,
                cancellationToken);

            if (created.IsFailure)
            {
                return Result.Failure<RegisterResponse>(created.Error!);
            }

            registered = created.Value;
        }

        var now = clock.UtcNow;
        foreach (var adminEmail in await users.GetEmailsInRoleAsync(Roles.Admin, cancellationToken))
        {
            db.OutboxMessages.Add(EmailOutbox.AdminNewRegistration(adminEmail, registered, now));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new RegisterResponse(UserStatus.PendingApproval));
    }
}
