namespace Mya.Application.Common.Results;

/// <summary>
/// A failure with a stable machine-readable <see cref="Code"/> (see <see cref="ErrorCodes"/>).
/// The Angular interceptor switches on the code, never on the title.
/// </summary>
public sealed record Error(string Code, string Title, ResultStatus Status)
{
    public string? Detail { get; init; }

    public Error WithDetail(string detail) => this with { Detail = detail };
}
