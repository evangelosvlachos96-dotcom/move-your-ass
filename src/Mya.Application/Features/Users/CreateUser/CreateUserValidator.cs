using FluentValidation;
using Mya.Application.Common.Validation;

namespace Mya.Application.Features.Users.CreateUser;

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.FirstName).PersonName();
        RuleFor(x => x.LastName).PersonName();
        RuleFor(x => x.Role!).Role().When(x => x.Role is not null);
    }
}
