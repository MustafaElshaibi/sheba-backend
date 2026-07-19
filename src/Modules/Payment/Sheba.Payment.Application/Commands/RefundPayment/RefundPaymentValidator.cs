using FluentValidation;

namespace Sheba.Payment.Application.Commands.RefundPayment;

public sealed class RefundPaymentValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentValidator()
    {
        RuleFor(x => x.PaymentOrderId).NotEmpty();
    }
}
