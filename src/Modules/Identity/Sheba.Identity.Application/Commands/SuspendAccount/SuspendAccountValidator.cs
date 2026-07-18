using FluentValidation;

namespace Sheba.Identity.Application.Commands.SuspendAccount;

public sealed class SuspendAccountValidator : AbstractValidator<SuspendAccountCommand>
{
    public SuspendAccountValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}
