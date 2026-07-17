using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.EnrollAdminMfa;

/// <summary>
/// Step 1 of TOTP enrollment (T-SEC-1): generates a new secret for the calling admin and stores
/// it encrypted but unconfirmed — MFA does not gate login until ConfirmAdminMfaCommand proves the
/// authenticator app actually has it. AdminId always comes from the caller's own verified token;
/// there is no path for one admin to enroll MFA on another's account.
///
/// API: POST /api/admin/mfa/enroll
/// </summary>
public sealed record EnrollAdminMfaCommand(Guid AdminId) : IRequest<Result<EnrollAdminMfaResponse>>;

/// <summary>
/// Secret + provisioning URI are shown exactly once. The client renders ProvisioningUri as a QR
/// code (or offers Secret for manual entry), then must call ConfirmAdminMfaCommand with a live
/// code from the app before MFA is actually enforced.
/// </summary>
public sealed record EnrollAdminMfaResponse(string Secret, string ProvisioningUri);
