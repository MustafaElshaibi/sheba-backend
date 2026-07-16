using FluentValidation;

namespace Sheba.Identity.Application.Commands.RegisterCitizen;

public sealed class RegisterCitizenValidator : AbstractValidator<RegisterCitizenCommand>
{
    public RegisterCitizenValidator()
    {
        RuleFor(x => x.NationalId)
            .NotEmpty().WithMessage("National ID is required.")
            .Length(10, 20).WithMessage("National ID must be between 10 and 20 characters.")
            .Matches(@"^\d+$").WithMessage("National ID must contain only digits.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^(\+967|967|0)\d{9}$").WithMessage("Phone number must be a valid Yemeni number (+967xxxxxxxxx).");
    }
}
