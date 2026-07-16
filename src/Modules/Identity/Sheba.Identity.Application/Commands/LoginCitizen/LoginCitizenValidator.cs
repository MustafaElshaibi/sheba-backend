using FluentValidation;

namespace Sheba.Identity.Application.Commands.LoginCitizen;

/// <summary>
/// Validates login step 1 input. Guarantees the handler never receives a null/blank
/// identifier or password, so a malformed request returns 422 (validation) rather
/// than 500 (unhandled).
/// </summary>
public sealed class LoginCitizenValidator : AbstractValidator<LoginCitizenCommand>
{
    public LoginCitizenValidator()
    {
        RuleFor(x => x.UsernameOrNid)
            .NotEmpty().WithMessage("National ID or username is required.")
            .MaximumLength(100).WithMessage("National ID or username is too long.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(256).WithMessage("Password is too long.");
    }
}
