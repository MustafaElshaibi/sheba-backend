using MediatR;

namespace Sheba.Identity.Application.Commands.VerifyLoginOtp;

/// <summary>
/// Login step 2: citizen submits the OTP dispatched by LoginCitizenCommand.
/// On success the account's failed-login counters reset and last-login is recorded.
///
/// This does NOT itself mint OAuth tokens — it validates the second factor and
/// returns the verified subject + claims. The OpenIddict /connect/token endpoint
/// (custom grant urn:sheba:grant:national_id_otp) calls this and turns the result
/// into a signed access_token / id_token / refresh_token.
///
/// API: POST /api/identity/login/verify-otp
/// Body: { accountId, otp }
/// </summary>
public sealed record VerifyLoginOtpCommand(
    Guid   AccountId,
    string Otp
) : IRequest<VerifyLoginOtpResponse>;

public sealed record VerifyLoginOtpResponse(
    bool    Succeeded,
    Guid    AccountId,
    string? NationalId,
    string? Username,
    string? Email,
    string? FullNameEn,
    int     IdentityLevel,
    string? Message = null
);
