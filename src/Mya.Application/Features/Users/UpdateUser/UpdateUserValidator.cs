using FluentValidation;
using Mya.Application.Common.Validation;

namespace Mya.Application.Features.Users.UpdateUser;

/// <summary>Validates the wire shape; the action filter runs it on the bound request body.</summary>
public sealed class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.FirstName).PersonName();
        RuleFor(x => x.LastName).PersonName();
        RuleFor(x => x.Role).NotEmpty().Role();
    }
}
