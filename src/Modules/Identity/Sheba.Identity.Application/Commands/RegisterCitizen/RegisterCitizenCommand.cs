using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.RegisterCitizen;

/// <summary>
/// Step 1 of citizen registration: validates NID against civil registry,
/// creates a PendingVerification account, sends OTP to registered phone.
///
/// Returns: the new account ID so the frontend can poll for OTP verification.
///
/// API: POST /api/identity/register
/// Body: { nationalId, phoneNumber }
/// </summary>
public sealed record RegisterCitizenCommand(
    string NationalId,
    string PhoneNumber
) : IRequest<Result<RegisterCitizenResponse>>;

public sealed record RegisterCitizenResponse(
    Guid AccountId,
    string MaskedPhone    // e.g. "+967 777 ***456" — never reveal full number
);
