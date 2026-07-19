using MediatR;
using Sheba.Payment.Application.Common;
using Sheba.Payment.Domain.Entities;
using Sheba.Payment.Domain.Interfaces;

namespace Sheba.Payment.Application.Commands.CreatePaymentOrder;

public sealed class CreatePaymentOrderHandler(IPaymentRepository repository)
    : IRequestHandler<CreatePaymentOrderCommand, PaymentOrderDto>
{
    public async Task<PaymentOrderDto> Handle(CreatePaymentOrderCommand command, CancellationToken ct)
    {
        var order = PaymentOrder.Create(
            command.ServiceRequestId, command.CitizenId, command.TotalAmount, command.Currency, command.Description);

        await repository.AddAsync(order, ct);
        await repository.SaveChangesAsync(ct);

        return PaymentOrderDto.FromEntity(order);
    }
}
