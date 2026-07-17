using FluentValidation;

namespace Sheba.Identity.Application.Commands.CreateAdminUser;

public sealed class CreateAdminUserValidator : AbstractValidator<CreateAdminUserCommand>
{
    public CreateAdminUserValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(256);
        RuleFor(x => x.Department).MaximumLength(100);
        RuleFor(x => x.Role).IsInEnum();
    }
}
