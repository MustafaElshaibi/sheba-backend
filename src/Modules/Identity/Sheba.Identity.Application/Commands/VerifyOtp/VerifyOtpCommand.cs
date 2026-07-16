using MediatR;

namespace Sheba.Identity.Application.Commands.VerifyOtp;

/// <summary>
/// Step 2 of citizen registration: citizen submits the OTP received via SMS.
/// Validates the code and marks the account phone as verified.
///
/// API: POST /api/identity/verify-otp
/// Body: { accountId, otp }
/// </summary>
public sealed record VerifyOtpCommand(
    Guid   AccountId,
    string Otp
) : IRequest<VerifyOtpResponse>;

/// <summary>
/// Response for VerifyOtpCommand.
/// Succeeded = true means OTP matched and phone is now verified.
/// Message carries a user-facing description (success or failure reason).
/// </summary>
public sealed record VerifyOtpResponse(bool Succeeded, string? Message = null);
