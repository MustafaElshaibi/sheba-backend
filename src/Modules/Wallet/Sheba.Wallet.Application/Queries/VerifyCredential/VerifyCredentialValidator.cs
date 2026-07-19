using FluentValidation;

namespace Sheba.Wallet.Application.Queries.VerifyCredential;

public sealed class VerifyCredentialValidator : AbstractValidator<VerifyCredentialQuery>
{
    public VerifyCredentialValidator()
    {
        RuleFor(x => x.Jwt).NotEmpty().WithMessage("A credential JWT is required.");
    }
}
