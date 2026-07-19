using FluentValidation;

namespace Sheba.Payment.Application.Commands.ConfirmPayment;

public sealed class ConfirmPaymentValidator : AbstractValidator<ConfirmPaymentCommand>
{
    public ConfirmPaymentValidator()
    {
        RuleFor(x => x.PaymentOrderId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
    }
}
