using Mya.Domain.Enums;

namespace Mya.Application.Features.Users.ListUsers;

/// <summary>Bound from the query string, hence a class with defaults rather than a positional record.</summary>
public sealed class ListUsersQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public UserStatus? Status { get; init; }

    public string? Search { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;
}
