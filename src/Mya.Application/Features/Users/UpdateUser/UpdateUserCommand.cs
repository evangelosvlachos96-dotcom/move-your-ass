namespace Mya.Application.Features.Users.UpdateUser;

/// <summary>Body of PUT /api/admin/users/{id}; the controller fills <see cref="UserId"/> from the route.</summary>
public sealed record UpdateUserCommand(string FirstName, string LastName, string Role)
{
    public string UserId { get; init; } = string.Empty;
}
