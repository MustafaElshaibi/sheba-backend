using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.ConfirmPasswordReset;

/// <summary>
/// Step 2 of password reset: citizen supplies the OTP sent to their registered phone plus a new
/// password. Every failure path (unknown identifier, no active OTP, expired, too many attempts,
/// wrong code) returns the same generic error — the confirm step is just as identifier-based as
/// the request step, so it gets the same BR-ON-3 anti-enumeration treatment.
///
/// API: POST /api/identity/password-reset/confirm
/// </summary>
public sealed record ConfirmPasswordResetCommand(
    string UsernameOrNid,
    string Otp,
    string NewPassword,
    string ConfirmNewPassword
) : IRequest<Result<ConfirmPasswordResetResponse>>;

public sealed record ConfirmPasswordResetResponse(string Message);
