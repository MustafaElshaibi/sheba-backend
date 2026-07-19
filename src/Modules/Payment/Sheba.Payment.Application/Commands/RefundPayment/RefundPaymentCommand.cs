using MediatR;
using Sheba.Payment.Application.Common;

namespace Sheba.Payment.Application.Commands.RefundPayment;

/// <summary>System-Admin-only refund of a completed payment order (BR-PA-3, T-PAY-1).
/// Authorization is enforced at the endpoint (SuperAdminOnly policy) — no ownership check
/// needed here.</summary>
public sealed record RefundPaymentCommand(Guid PaymentOrderId) : IRequest<PaymentOrderDto>;
