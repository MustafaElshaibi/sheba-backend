using FluentValidation;

namespace Sheba.Identity.Application.Commands.VerifyLoginOtp;

/// <summary>Validates login OTP verification input (login step 2 / token grant).</summary>
public sealed class VerifyLoginOtpValidator : AbstractValidator<VerifyLoginOtpCommand>
{
    public VerifyLoginOtpValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("Account ID is required.");

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("OTP code is required.")
            .Matches(@"^\d{6}$").WithMessage("OTP must be a 6-digit code.");
    }
}
