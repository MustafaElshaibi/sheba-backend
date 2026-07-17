using MediatR;
using Sheba.Shared.Kernel.Results;

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
) : IRequest<Result<LoginCitizenResponse>>;

/// <summary>Response for a successful LoginCitizenCommand — failures are carried by Result&lt;T&gt;.Error.</summary>
public sealed record LoginCitizenResponse(
    Guid   AccountId,
    string MaskedPhone
);
