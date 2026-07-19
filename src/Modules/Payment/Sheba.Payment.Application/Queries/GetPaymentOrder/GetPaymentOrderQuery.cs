using MediatR;
using Sheba.Payment.Application.Common;

namespace Sheba.Payment.Application.Queries.GetPaymentOrder;

public sealed record GetPaymentOrderQuery(Guid PaymentOrderId, Guid ActorId, bool IsAdmin) : IRequest<PaymentOrderDto>;
