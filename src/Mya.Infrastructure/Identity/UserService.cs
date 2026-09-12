using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Paging;
using Mya.Application.Common.Results;
using Mya.Domain.Constants;
using Mya.Domain.Enums;
using Mya.Infrastructure.Persistence;

namespace Mya.Infrastructure.Identity;

/// <summary>
/// <see cref="IUserService"/> over Identity's UserManager and the shared scoped AppDbContext, so a
/// handler's transaction covers both the user change and its outbox rows.
/// </summary>
public sealed class UserService(UserManager<AppUser> userManager, AppDbContext db, IClock clock) : IUserService
{
    private const int UserAgentMaxLength = 256;
    private const int SqlServerDuplicateKey = 2601;
    private const int SqlServerUniqueConstraint = 2627;

    public async Task<UserAccount?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : await ToAccountAsync(user);
    }

    public async Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        return user is null ? null : await ToAccountAsync(user);
    }

    public async Task<PagedResult<UserAccount>> ListAsync(UserListFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = UsersWithRole();

        if (filter.Status is { } status)
        {
            query = query.Where(x => x.User.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x =>
                x.User.Email!.Contains(term)
                || x.User.FirstName.Contains(term)
                || x.User.LastName.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.User.CreatedAtUtc)
            .ThenBy(x => x.User.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserAccount>(
            rows.Select(x => ToAccount(x.User, x.Role)).ToList(),
            filter.Page,
            filter.PageSize,
            total);
    }

    public Task<int> CountInRoleAsync(string role, CancellationToken cancellationToken) =>
        UsersWithRole().CountAsync(x => x.Role == role, cancellationToken);

    public async Task<IReadOnlyList<string>> GetEmailsInRoleAsync(string role, CancellationToken cancellationToken) =>
        await UsersWithRole()
            .Where(x => x.Role == role && x.User.Email != null)
            .Select(x => x.User.Email!)
            .ToListAsync(cancellationToken);

    public async Task<Result<UserAccount>> CreateAsync(NewUserAccount account, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        var now = clock.UtcNow;
        var user = new AppUser
        {
            UserName = account.Email,
            Email = account.Email,
            EmailConfirmed = true,
            FirstName = account.FirstName,
            LastName = account.LastName,
            Status = account.Status,
            MustChangePassword = account.MustChangePassword,
            CreatedAtUtc = now,
            ApprovedAtUtc = account.Status == UserStatus.Active ? now : null,
        };

        IdentityResult created;
        try
        {
            created = await userManager.CreateAsync(user, password);
        }
        catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))
        {
            // The unique index on the normalised user name (= email) is the guarantee.
            return Result.Failure<UserAccount>(Errors.EmailAlreadyExists);
        }

        if (!created.Succeeded)
        {
            return Result.Failure<UserAccount>(
                IsDuplicate(created)
                    ? Errors.EmailAlreadyExists
                    : Errors.ValidationFailed.WithDetail(created.Describe()));
        }

        (await userManager.AddToRoleAsync(user, account.Role)).ThrowIfFailed($"assign role '{account.Role}'");

        return Result.Success(ToAccount(user, account.Role));
    }

    public async Task<PasswordCheckOutcome> CheckPasswordAsync(string userId, string password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return PasswordCheckOutcome.Invalid;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return PasswordCheckOutcome.LockedOut;
        }

        if (await userManager.CheckPasswordAsync(user, password))
        {
            if (user.AccessFailedCount > 0)
            {
                await userManager.ResetAccessFailedCountAsync(user);
            }

            return PasswordCheckOutcome.Success;
        }

        // Counts towards lockout: 5 failures lock the account for 15 minutes (IdentityOptionsSetup).
        await userManager.AccessFailedAsync(user);
        return PasswordCheckOutcome.Invalid;
    }

    public async Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userId);
        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);

        if (result.Succeeded)
        {
            return Result.Success();
        }

        return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
            ? Result.Failure(Errors.CurrentPasswordWrong)
            : Result.Failure(Errors.ValidationFailed.WithDetail(result.Describe()));
    }

    public async Task ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userId);

        if (await userManager.HasPasswordAsync(user))
        {
            (await userManager.RemovePasswordAsync(user)).ThrowIfFailed("remove the old password");
        }

        (await userManager.AddPasswordAsync(user, newPassword)).ThrowIfFailed("set the new password");
        (await userManager.UpdateSecurityStampAsync(user)).ThrowIfFailed("rotate the security stamp");
    }

    public async Task UpdateNamesAsync(string userId, string firstName, string lastName, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userId);
        user.FirstName = firstName;
        user.LastName = lastName;
        (await userManager.UpdateAsync(user)).ThrowIfFailed("update names");
    }

    public async Task SetRoleAsync(string userId, string role, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userId);

        var current = await userManager.GetRolesAsync(user);
        if (current.Count > 0)
        {
            (await userManager.RemoveFromRolesAsync(user, current)).ThrowIfFailed("remove current roles");
        }

        (await userManager.AddToRoleAsync(user, role)).ThrowIfFailed($"assign role '{role}'");
        (await userManager.UpdateSecurityStampAsync(user)).ThrowIfFailed("rotate the security stamp");
    }

    public async Task SetStatusAsync(string userId, UserStatus status, string? reason, string? actorUserId, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userId);
        var now = clock.UtcNow;

        switch (status)
        {
            case UserStatus.Active:
                if (user.Status == UserStatus.PendingApproval)
                {
                    user.ApprovedAtUtc = now;
                    user.ApprovedByUserId = actorUserId;
                }

                user.SuspendedAtUtc = null;
                user.SuspensionReason = null;
                break;

            case UserStatus.Suspended:
                user.SuspendedAtUtc = now;
                user.SuspensionReason = reason;
                break;

            case UserStatus.Declined:
            case UserStatus.PendingApproval:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown user status.");
        }

        user.Status = status;
        (await userManager.UpdateAsync(user)).ThrowIfFailed($"set status {status}");
    }

    public async Task SetMustChangePasswordAsync(string userId, bool mustChangePassword, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userId);
        user.MustChangePassword = mustChangePassword;
        (await userManager.UpdateAsync(user)).ThrowIfFailed("update MustChangePassword");
    }

    public async Task<Guid> StartSessionAsync(string userId, string? userAgent, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userId);
        var sessionId = Guid.NewGuid();

        user.ActiveSessionId = sessionId;
        user.ActiveSessionStartedAtUtc = clock.UtcNow;
        user.ActiveSessionUserAgent = userAgent is { Length: > UserAgentMaxLength }
            ? userAgent[..UserAgentMaxLength]
            : userAgent;

        (await userManager.UpdateAsync(user)).ThrowIfFailed("start session");
        return sessionId;
    }

    public async Task EndSessionAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userId);

        user.ActiveSessionId = null;
        user.ActiveSessionStartedAtUtc = null;
        user.ActiveSessionUserAgent = null;

        // UpdateSecurityStampAsync persists the entity as well as rotating the stamp.
        (await userManager.UpdateSecurityStampAsync(user)).ThrowIfFailed("end session");
    }

    public async Task DeleteAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userId);
        (await userManager.DeleteAsync(user)).ThrowIfFailed("delete user");
    }

    private IQueryable<UserWithRole> UsersWithRole() =>
        from user in db.Users
        join userRole in db.UserRoles on user.Id equals userRole.UserId into userRoles
        from userRole in userRoles.DefaultIfEmpty()
        join role in db.Roles on userRole!.RoleId equals role.Id into roles
        from role in roles.DefaultIfEmpty()
        select new UserWithRole { User = user, Role = role!.Name };

    private async Task<AppUser> RequireAsync(string userId) =>
        await userManager.FindByIdAsync(userId)
        ?? throw new InvalidOperationException($"User '{userId}' no longer exists.");

    private async Task<UserAccount> ToAccountAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return ToAccount(user, roles.FirstOrDefault());
    }

    private static UserAccount ToAccount(AppUser user, string? role) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.FirstName,
        user.LastName,
        role ?? Roles.Client,
        user.Status,
        user.MustChangePassword,
        user.ActiveSessionId,
        user.SecurityStamp ?? string.Empty,
        user.CreatedAtUtc,
        user.ApprovedAtUtc,
        user.SuspendedAtUtc,
        user.SuspensionReason);

    private static bool IsUniqueIndexViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: SqlServerDuplicateKey or SqlServerUniqueConstraint }
        || (exception.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.Ordinal) ?? false);

    private static bool IsDuplicate(IdentityResult result) =>
        result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.DuplicateEmail));

    private sealed class UserWithRole
    {
        public AppUser User { get; init; } = default!;

        public string? Role { get; init; }
    }
}
