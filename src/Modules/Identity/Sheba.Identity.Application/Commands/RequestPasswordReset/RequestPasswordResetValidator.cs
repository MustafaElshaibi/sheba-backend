using FluentValidation;

namespace Sheba.Identity.Application.Commands.RequestPasswordReset;

public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(x => x.UsernameOrNid)
            .NotEmpty().WithMessage("Username or National ID is required.")
            .MaximumLength(100).WithMessage("Identifier is too long.");
    }
}
