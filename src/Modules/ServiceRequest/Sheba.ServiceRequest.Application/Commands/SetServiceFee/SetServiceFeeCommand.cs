using MediatR;

namespace Sheba.ServiceRequest.Application.Commands.SetServiceFee;

public sealed record SetServiceFeeCommand(
    Guid ServiceId,
    string FeeType,        // BASE, EXPEDITE, DELIVERY
    string NameAr,
    string NameEn,
    decimal Amount,
    string Currency = "YER",
    bool IsMandatory = true
) : IRequest<SetServiceFeeResponse>;

public sealed record SetServiceFeeResponse(Guid FeeId, string Message);
