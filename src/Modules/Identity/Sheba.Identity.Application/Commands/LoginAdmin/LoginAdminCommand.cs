using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.LoginAdmin;

/// <summary>
/// Admin authentication — a separate principal type from citizen accounts (§10.1: "one human,
/// two principals, so admin privilege never rides on a citizen token"). Consumed by the
/// <c>urn:sheba:grant:admin_password</c> custom OIDC grant, mirroring how LoginCitizenCommand
/// backs the citizen custom grant.
///
/// Mandatory TOTP for admin sessions is designed (BR-LG-6) but deferred to T-SEC-1; this is the
/// password-only baseline it will sit in front of.
/// </summary>
public sealed record LoginAdminCommand(
    string EmployeeIdOrEmail,
    string Password
) : IRequest<Result<LoginAdminResponse>>;

/// <summary>Response for a successful LoginAdminCommand — failures are carried by Result&lt;T&gt;.Error.</summary>
public sealed record LoginAdminResponse(
    Guid AdminId,
    string Role,
    string FullName,
    string Email
);
