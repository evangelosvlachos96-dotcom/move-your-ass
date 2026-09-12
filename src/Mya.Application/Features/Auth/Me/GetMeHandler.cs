using AutoMapper;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Abstractions.System;
using Mya.Application.Common.Results;
using Mya.Application.Features.Common;

namespace Mya.Application.Features.Auth.Me;

/// <summary>Live state from the store, so the SPA sees MustChangePassword flip without a new token.</summary>
public sealed class GetMeHandler(IUserService users, ICurrentUser currentUser, IMapper mapper)
{
    public async Task<Result<UserDto>> Handle(CancellationToken cancellationToken)
    {
        var user = currentUser.UserId is { } id
            ? await users.FindByIdAsync(id, cancellationToken)
            : null;

        return user is null
            ? Result.Failure<UserDto>(Errors.UserNotFound)
            : Result.Success(mapper.Map<UserDto>(user));
    }
}
