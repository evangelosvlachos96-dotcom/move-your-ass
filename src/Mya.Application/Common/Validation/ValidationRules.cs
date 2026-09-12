using FluentValidation;
using Mya.Domain.Constants;

namespace Mya.Application.Common.Validation;

/// <summary>Shape rules shared by several commands. Invariants that need the database live in handlers.</summary>
public static class ValidationRules
{
    public const int NameMaxLength = 80;
    public const int EmailMaxLength = 256;
    public const int ReasonMaxLength = 500;
    public const int PasswordMinLength = 10;
    public const int PasswordMaxLength = 128;

    public static IRuleBuilderOptions<T, string> PersonName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(NameMaxLength);

    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().EmailAddress().MaximumLength(EmailMaxLength);

    /// <summary>Mirrors the Identity password policy so a bad password fails before hitting Identity.</summary>
    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(PasswordMinLength)
            .MaximumLength(PasswordMaxLength)
            .Matches("[A-Z]").WithMessage("Must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Must contain a digit.");

    public static IRuleBuilderOptions<T, string> Role<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(role => role is Roles.Admin or Roles.Client)
            .WithMessage($"Role must be '{Roles.Admin}' or '{Roles.Client}'.");

    public static IRuleBuilderOptions<T, string?> OptionalReason<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(ReasonMaxLength);
}
