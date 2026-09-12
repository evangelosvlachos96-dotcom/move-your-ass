using AutoMapper;
using Mya.Application.Abstractions.Identity;
using Mya.Domain.Enums;

namespace Mya.Application.Features.Common;

/// <summary>Wire shape of a user for /auth/me and the admin list. Never carries secrets or stamps.</summary>
public sealed record UserDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    UserStatus Status,
    bool MustChangePassword,
    DateTime CreatedAtUtc,
    DateTime? ApprovedAtUtc,
    DateTime? SuspendedAtUtc,
    string? SuspensionReason);

public sealed class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<UserAccount, UserDto>();
    }
}
