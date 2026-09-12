namespace Mya.Application.Features.Users.CreateUser;

/// <summary>Role defaults to Client when omitted.</summary>
public sealed record CreateUserCommand(string Email, string FirstName, string LastName, string? Role);

/// <summary>The temporary password is returned exactly once and never stored in clear.</summary>
public sealed record CreateUserResponse(string Id, string TemporaryPassword);
