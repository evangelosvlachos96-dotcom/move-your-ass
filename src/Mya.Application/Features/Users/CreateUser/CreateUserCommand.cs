namespace Mya.Application.Features.Users.CreateUser;

/// <summary>Role defaults to Client when omitted.</summary>
public sealed record CreateUserCommand(string Email, string FirstName, string LastName, string? Role);

/// <summary>The setup credential is delivered only through the email outbox.</summary>
public sealed record CreateUserResponse(string Id);
