using FluentValidation;

namespace Sheba.Identity.Application.Commands.ReinstateAccount;

public sealed class ReinstateAccountValidator : AbstractValidator<ReinstateAccountCommand>
{
    public ReinstateAccountValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
    }
}
