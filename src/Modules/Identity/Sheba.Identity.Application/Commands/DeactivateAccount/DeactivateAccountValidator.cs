using FluentValidation;

namespace Sheba.Identity.Application.Commands.DeactivateAccount;

public sealed class DeactivateAccountValidator : AbstractValidator<DeactivateAccountCommand>
{
    public DeactivateAccountValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}
