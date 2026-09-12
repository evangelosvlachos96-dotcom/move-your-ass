using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.Persistence;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Notifications;
using Mya.Application.Common.Results;
using Mya.Domain.Constants;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Auth.Register;

/// <summary>Self-service path: the account is created pending and every Admin is emailed.</summary>
public sealed class RegisterHandler(IUserService users, IAppDbContext db, IClock clock)
{
    public async Task<Result<RegisterResponse>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var created = await users.CreateAsync(
            new NewUserAccount(
                command.Email.Trim(),
                command.FirstName.Trim(),
                command.LastName.Trim(),
                Roles.Client,
                UserStatus.PendingApproval,
                MustChangePassword: false),
            command.Password,
            cancellationToken);

        if (created.IsFailure)
        {
            return Result.Failure<RegisterResponse>(created.Error!);
        }

        var now = clock.UtcNow;
        foreach (var adminEmail in await users.GetEmailsInRoleAsync(Roles.Admin, cancellationToken))
        {
            db.OutboxMessages.Add(EmailOutbox.AdminNewRegistration(adminEmail, created.Value, now));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new RegisterResponse(UserStatus.PendingApproval));
    }
}
