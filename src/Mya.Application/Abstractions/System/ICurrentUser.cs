namespace Mya.Application.Abstractions.System;

/// <summary>The caller of the current request, as established by the host's authentication.</summary>
public interface ICurrentUser
{
    /// <summary>Null when the request is anonymous.</summary>
    public string? UserId { get; }

    public string? UserAgent { get; }

    public string? IpAddress { get; }
}
