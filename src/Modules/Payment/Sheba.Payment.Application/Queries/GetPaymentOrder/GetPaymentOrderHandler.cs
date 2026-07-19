using MediatR;
using Sheba.Payment.Application.Common;
using Sheba.Payment.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Payment.Application.Queries.GetPaymentOrder;

public sealed class GetPaymentOrderHandler(IPaymentRepository repository)
    : IRequestHandler<GetPaymentOrderQuery, PaymentOrderDto>
{
    public async Task<PaymentOrderDto> Handle(GetPaymentOrderQuery query, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(query.PaymentOrderId, ct)
            ?? throw new NotFoundException("PaymentOrder", query.PaymentOrderId);

        if (!query.IsAdmin && order.CitizenId != query.ActorId)
            throw new NotFoundException("PaymentOrder", query.PaymentOrderId);

        return PaymentOrderDto.FromEntity(order);
    }
}
