using AutoMapper;
using Mya.Application.Abstractions.Identity;
using Mya.Application.Common.Paging;
using Mya.Application.Common.Results;
using Mya.Application.Features.Common;

namespace Mya.Application.Features.Users.ListUsers;

public sealed class ListUsersHandler(IUserService users, IMapper mapper)
{
    public async Task<Result<PagedResult<UserDto>>> Handle(ListUsersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = await users.ListAsync(
            new UserListFilter(query.Status, query.Search, query.Page, query.PageSize),
            cancellationToken);

        var items = page.Items.Select(mapper.Map<UserDto>).ToList();
        return Result.Success(new PagedResult<UserDto>(items, page.Page, page.PageSize, page.TotalCount));
    }
}
