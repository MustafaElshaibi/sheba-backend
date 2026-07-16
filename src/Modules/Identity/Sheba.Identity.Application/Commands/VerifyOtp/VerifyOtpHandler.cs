using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Shared.Kernel.Interfaces;

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
) : IRequestHandler<VerifyOtpCommand, VerifyOtpResponse>
{
    private const int MaxAttempts = 3;

    public async Task<VerifyOtpResponse> Handle(
        VerifyOtpCommand request,
        CancellationToken cancellationToken)
    {
        var account = await repository.FindAccountByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Account), request.AccountId);

        // Only pending-verification accounts may submit an OTP
        if (account.Status != AccountStatus.PendingVerification)
        {
            throw new DomainException(
                $"Account is in status {account.Status} — OTP verification is not applicable.");
        }

        var otpRecord = await repository.FindActiveOtpAsync(
            request.AccountId, OtpPurpose.Registration, cancellationToken);

        if (otpRecord is null)
        {
            return new VerifyOtpResponse(Succeeded: false, Message: "No active OTP found. Please request a new code.");
        }

        if (otpRecord.IsExpired())
        {
            return new VerifyOtpResponse(Succeeded: false, Message: "The verification code has expired. Please request a new one.");
        }

        if (otpRecord.AttemptCount >= MaxAttempts)
        {
            return new VerifyOtpResponse(Succeeded: false, Message: "Too many attempts. Please request a new verification code.");
        }

        // Increment attempt before validating (prevents timing-based enumeration)
        otpRecord.RecordAttempt();

        if (!otpHasher.Verify(request.Otp, otpRecord.CodeHash))
        {
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "[VerifyOtp] Invalid OTP for AccountId={AccountId}, Attempt={Attempt}",
                request.AccountId, otpRecord.AttemptCount);

            int remaining = MaxAttempts - otpRecord.AttemptCount;
            return new VerifyOtpResponse(Succeeded: false,
                Message: remaining > 0
                    ? $"Invalid code. {remaining} attempt(s) remaining."
                    : "Too many invalid attempts. Please request a new code.");
        }

        // ── Success ──────────────────────────────────────────────────────────
        otpRecord.MarkUsed();
        account.VerifyPhone();

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[VerifyOtp] Phone verified successfully for AccountId={AccountId}",
            request.AccountId);

        return new VerifyOtpResponse(Succeeded: true, Message: "Phone number verified successfully.");
    }
}
