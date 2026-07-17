using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.LoginAdmin;

/// <summary>
/// Admin authentication — a separate principal type from citizen accounts (§10.1: "one human,
/// two principals, so admin privilege never rides on a citizen token"). Consumed by the
/// <c>urn:sheba:grant:admin_password</c> custom OIDC grant, mirroring how LoginCitizenCommand
/// backs the citizen custom grant.
///
/// Mandatory TOTP for admins who have completed enrollment (BR-LG-6 / T-SEC-1): once
/// <c>AdminUser.MfaEnabled</c> is true, a missing/invalid <see cref="MfaCode"/> fails the login
/// with a distinguishable error so the client can re-prompt and resubmit. Admins who have not
/// yet enrolled keep the password-only baseline (enrollment itself requires an authenticated
/// admin token, so it cannot be a login precondition — see docs/sheba.md §6.7).
/// <see cref="MfaCode"/> accepts either a live 6-digit TOTP code or a single-use recovery code.
/// </summary>
public sealed record LoginAdminCommand(
    string EmployeeIdOrEmail,
    string Password,
    string? MfaCode = null
) : IRequest<Result<LoginAdminResponse>>;

/// <summary>Response for a successful LoginAdminCommand — failures are carried by Result&lt;T&gt;.Error.</summary>
public sealed record LoginAdminResponse(
    Guid AdminId,
    string Role,
    string FullName,
    string Email,
    Guid? MinistryId
);
