using FluentValidation;

namespace Sheba.Identity.Application.Commands.ConfirmPasswordReset;

/// <summary>Mirrors CompleteRegistrationValidator's password policy so a reset password meets the
/// same strength bar as one set at registration.</summary>
public sealed class ConfirmPasswordResetValidator : AbstractValidator<ConfirmPasswordResetCommand>
{
    public ConfirmPasswordResetValidator()
    {
        RuleFor(x => x.UsernameOrNid)
            .NotEmpty().WithMessage("Username or National ID is required.");

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("Reset code is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(256).WithMessage("Password is too long.")
            .Matches(@"[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches(@"\d").WithMessage("Password must contain a digit.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain a special character.");

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("Password confirmation does not match.");
    }
}
