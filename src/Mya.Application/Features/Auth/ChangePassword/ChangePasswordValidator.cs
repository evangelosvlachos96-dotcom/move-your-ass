using FluentValidation;
using Mya.Application.Common.Validation;

namespace Mya.Application.Features.Auth.ChangePassword;

/// <summary>
/// Shape only. Whether <c>CurrentPassword</c> is required depends on the user's
/// <c>MustChangePassword</c> flag, which the handler reads from the database.
/// </summary>
public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .MaximumLength(ValidationRules.PasswordMaxLength)
            .When(x => x.CurrentPassword is not null);

        RuleFor(x => x.NewPassword).Password();

        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword!)
            .When(x => !string.IsNullOrEmpty(x.CurrentPassword))
            .WithMessage("The new password must differ from the current one.");
    }
}
