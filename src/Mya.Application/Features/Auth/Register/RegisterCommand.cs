using Mya.Domain.Enums;

namespace Mya.Application.Features.Auth.Register;

public sealed record RegisterCommand(string Email, string FirstName, string LastName, string Password);

/// <summary>202: nothing to log into yet. The admin is notified and must approve first.</summary>
public sealed record RegisterResponse(UserStatus Status);
