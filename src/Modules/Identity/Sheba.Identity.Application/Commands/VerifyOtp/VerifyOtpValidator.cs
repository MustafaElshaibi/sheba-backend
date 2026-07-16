using FluentValidation;

namespace Sheba.Identity.Application.Commands.VerifyOtp;

/// <summary>Validates OTP verification input (registration step 2).</summary>
public sealed class VerifyOtpValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("Account ID is required.");

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("OTP code is required.")
            .Matches(@"^\d{6}$").WithMessage("OTP must be a 6-digit code.");
    }
}
