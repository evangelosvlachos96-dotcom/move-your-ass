namespace Mya.Application.Features.Users.UpdateUser;

/// <summary>Body of PUT /api/admin/users/{id}. The target user comes from the route only.</summary>
public sealed record UpdateUserRequest(string FirstName, string LastName, string Role);

/// <summary>Built by the controller from the route id and the <see cref="UpdateUserRequest"/> body.</summary>
public sealed record UpdateUserCommand(string FirstName, string LastName, string Role)
{
    public string UserId { get; init; } = string.Empty;
}
