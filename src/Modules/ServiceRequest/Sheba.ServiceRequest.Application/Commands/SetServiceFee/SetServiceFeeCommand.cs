using MediatR;

namespace Sheba.ServiceRequest.Application.Commands.SetServiceFee;

public sealed record SetServiceFeeCommand(
    Guid ServiceId,
    string FeeType,        // BASE, EXPEDITE, DELIVERY
    string NameAr,
    string NameEn,
    decimal Amount,
    string Currency = "YER",
    bool IsMandatory = true,
    // T-AUTH-1: null for SuperAdmin (unrestricted); a MinistryManager's own ministry_id
    // otherwise — the handler rejects adding a fee to a service owned by a different ministry.
    Guid? ActorMinistryId = null
) : IRequest<SetServiceFeeResponse>;

public sealed record SetServiceFeeResponse(Guid FeeId, string Message);
