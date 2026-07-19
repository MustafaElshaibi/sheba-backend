using FluentValidation;

namespace Sheba.Payment.Application.Commands.CreatePaymentOrder;

public sealed class CreatePaymentOrderValidator : AbstractValidator<CreatePaymentOrderCommand>
{
    public CreatePaymentOrderValidator()
    {
        RuleFor(x => x.ServiceRequestId).NotEmpty();
        RuleFor(x => x.CitizenId).NotEmpty();
        RuleFor(x => x.TotalAmount).GreaterThan(0).WithMessage("Total amount must be positive.");
        RuleFor(x => x.Currency).NotEmpty().Length(3).WithMessage("Currency must be a 3-letter ISO code.");
    }
}
