using MediatR;
using Sheba.Payment.Application.Common;

namespace Sheba.Payment.Application.Commands.CreatePaymentOrder;

/// <summary>
/// Creates a payment order for a service request's Payment workflow step. Called only from
/// <c>PaymentOrderPortAdapter</c> (the Shared.Kernel <c>IPaymentOrderPort</c> implementation) —
/// never exposed as a citizen-facing endpoint, so no ownership check here; the caller (the
/// ServiceRequest module, via the port) is already trusted.
/// </summary>
public sealed record CreatePaymentOrderCommand(
    Guid ServiceRequestId,
    Guid CitizenId,
    decimal TotalAmount,
    string Currency,
    string? Description
) : IRequest<PaymentOrderDto>;
