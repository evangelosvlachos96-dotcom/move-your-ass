using FluentValidation;
using Mya.Application.Common.Validation;

namespace Mya.Application.Features.Auth.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(ValidationRules.EmailMaxLength);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(ValidationRules.PasswordMaxLength);
    }
}
