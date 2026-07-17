using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.VerifyOtp;

/// <summary>
/// Validates the citizen's submitted OTP:
/// 1. Load account + active OTP record
/// 2. Check: not expired, attempts ≤ 3, code hash matches
/// 3. Mark OTP as used; mark Account phone as verified
/// 4. Persist
/// </summary>
public sealed class VerifyOtpHandler(
    IIdentityRepository repository,
    IOtpHasher otpHasher,
    ILogger<VerifyOtpHandler> logger
) : IRequestHandler<VerifyOtpCommand, Result<VerifyOtpResponse>>
{
    private const int MaxAttempts = 3;

    public async Task<Result<VerifyOtpResponse>> Handle(
        VerifyOtpCommand request,
        CancellationToken cancellationToken)
    {
        var account = await repository.FindAccountByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result.Failure<VerifyOtpResponse>(Error.NotFound("resource", "Account not found."));

        // Only pending-verification accounts may submit an OTP
        if (account.Status != AccountStatus.PendingVerification)
        {
            return Result.Failure<VerifyOtpResponse>(Error.Conflict(
                "domain", $"Account is in status {account.Status} — OTP verification is not applicable."));
        }

        var otpRecord = await repository.FindActiveOtpAsync(
            request.AccountId, OtpPurpose.Registration, cancellationToken);

        if (otpRecord is null)
            return Result.Failure<VerifyOtpResponse>(Error.Validation("otp", "No active OTP found. Please request a new code."));

        if (otpRecord.IsExpired())
            return Result.Failure<VerifyOtpResponse>(Error.Validation("otp", "The verification code has expired. Please request a new one."));

        if (otpRecord.AttemptCount >= MaxAttempts)
            return Result.Failure<VerifyOtpResponse>(Error.Validation("otp", "Too many attempts. Please request a new verification code."));

        // Increment attempt before validating (prevents timing-based enumeration)
        otpRecord.RecordAttempt();

        if (!otpHasher.Verify(request.Otp, otpRecord.CodeHash))
        {
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "[VerifyOtp] Invalid OTP for AccountId={AccountId}, Attempt={Attempt}",
                request.AccountId, otpRecord.AttemptCount);

            int remaining = MaxAttempts - otpRecord.AttemptCount;
            return Result.Failure<VerifyOtpResponse>(Error.Validation("otp", remaining > 0
                ? $"Invalid code. {remaining} attempt(s) remaining."
                : "Too many invalid attempts. Please request a new code."));
        }

        // ── Success ──────────────────────────────────────────────────────────
        otpRecord.MarkUsed();
        account.VerifyPhone();

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[VerifyOtp] Phone verified successfully for AccountId={AccountId}",
            request.AccountId);

        return Result.Success(new VerifyOtpResponse("Phone number verified successfully."));
    }
}
