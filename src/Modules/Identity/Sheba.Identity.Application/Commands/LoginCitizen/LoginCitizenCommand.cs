using MediatR;

namespace Sheba.Identity.Application.Commands.LoginCitizen;

/// <summary>
/// Step 1 of citizen login: validate credentials and dispatch an OTP.
/// Supports login by National ID or username + password.
///
/// API: POST /api/identity/login
/// Body: { usernameOrNid, password }
///
/// Note: the actual token issuance happens via OpenIddict's token endpoint.
/// This command handles the custom NID+OTP grant type pre-check.
/// </summary>
public sealed record LoginCitizenCommand(
    string UsernameOrNid,
    string Password
) : IRequest<LoginCitizenResponse>;

public sealed record LoginCitizenResponse(
    bool   OtpSent,
    Guid   AccountId,
    string MaskedPhone,
    string? FailureReason = null
);
