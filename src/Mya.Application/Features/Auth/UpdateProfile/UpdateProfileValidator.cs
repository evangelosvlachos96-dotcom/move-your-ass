using FluentValidation;
using Mya.Application.Common.Validation;

namespace Mya.Application.Features.Auth.UpdateProfile;

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.FirstName).PersonName();
        RuleFor(x => x.LastName).PersonName();
    }
}
