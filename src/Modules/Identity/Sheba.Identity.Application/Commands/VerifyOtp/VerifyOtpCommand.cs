using MediatR;
using Sheba.Shared.Kernel.Results;

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
) : IRequest<Result<VerifyOtpResponse>>;

/// <summary>Response for a successful VerifyOtpCommand — failures are carried by Result&lt;T&gt;.Error.</summary>
public sealed record VerifyOtpResponse(string Message);
