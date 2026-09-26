using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.System;
using Mya.Domain.Constants;
using Mya.Domain.Enums;
namespace Mya.Application.Features.Videos;
public sealed class VideoAccess(IUserService users, ICurrentUser current)
{
    public async Task<bool> AllowedAsync(bool admin, CancellationToken ct)
    {
        if (current.UserId is null) return false;
        var user = await users.FindByIdAsync(current.UserId, ct);
        return user is { Status: UserStatus.Active, MustChangePassword: false } && (!admin || user.Role == Roles.Admin);
    }
}
