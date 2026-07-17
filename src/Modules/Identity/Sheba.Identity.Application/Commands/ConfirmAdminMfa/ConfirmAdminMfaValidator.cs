using FluentValidation;

namespace Sheba.Identity.Application.Commands.ConfirmAdminMfa;

public sealed class ConfirmAdminMfaValidator : AbstractValidator<ConfirmAdminMfaCommand>
{
    public ConfirmAdminMfaValidator()
    {
        RuleFor(x => x.TotpCode)
            .NotEmpty().WithMessage("Verification code is required.")
            .Matches("^[0-9]{6}$").WithMessage("Verification code must be 6 digits.");
    }
}
