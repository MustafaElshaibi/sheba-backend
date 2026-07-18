using FluentValidation;

namespace Sheba.Citizen.Application.Commands.UpdateProfile;

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
        RuleFor(x => x.PhoneNumber).MaximumLength(20).When(x => x.PhoneNumber is not null);
        RuleFor(x => x.Address).MaximumLength(500).When(x => x.Address is not null);
        RuleFor(x => x.City).MaximumLength(100).When(x => x.City is not null);
        RuleFor(x => x.Governorate).MaximumLength(100).When(x => x.Governorate is not null);
    }
}
