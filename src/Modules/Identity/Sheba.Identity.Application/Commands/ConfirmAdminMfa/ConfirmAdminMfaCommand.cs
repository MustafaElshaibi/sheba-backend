using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.ConfirmAdminMfa;

/// <summary>
/// Step 2 of TOTP enrollment (T-SEC-1): proves the admin's authenticator app has the secret from
/// EnrollAdminMfaCommand, flips AdminUser.MfaEnabled on, and issues one batch of recovery codes.
///
/// API: POST /api/admin/mfa/verify
/// </summary>
public sealed record ConfirmAdminMfaCommand(
    Guid AdminId,
    string TotpCode
) : IRequest<Result<ConfirmAdminMfaResponse>>;

/// <summary>Recovery codes are returned exactly once — Sheba only ever persists their hashes.</summary>
public sealed record ConfirmAdminMfaResponse(IReadOnlyList<string> RecoveryCodes);
