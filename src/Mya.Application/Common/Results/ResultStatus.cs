namespace Mya.Application.Common.Results;

/// <summary>Outcome category of a handler. The API maps each to an HTTP status.</summary>
public enum ResultStatus
{
    Success,
    Invalid,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
}
