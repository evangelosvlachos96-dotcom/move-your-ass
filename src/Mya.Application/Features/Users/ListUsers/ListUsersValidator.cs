using FluentValidation;

namespace Mya.Application.Features.Users.ListUsers;

public sealed class ListUsersValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, ListUsersQuery.MaxPageSize);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
