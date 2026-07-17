using FluentValidation;

namespace Sheba.Identity.Application.Commands.LoginAdmin;

public sealed class LoginAdminValidator : AbstractValidator<LoginAdminCommand>
{
    public LoginAdminValidator()
    {
        RuleFor(x => x.EmployeeIdOrEmail)
            .NotEmpty().WithMessage("Employee ID or email is required.")
            .MaximumLength(200).WithMessage("Employee ID or email is too long.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(256).WithMessage("Password is too long.");
    }
}
