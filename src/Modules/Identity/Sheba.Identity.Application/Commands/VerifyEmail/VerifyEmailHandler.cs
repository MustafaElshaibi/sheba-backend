using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.VerifyEmail;

public sealed class VerifyEmailHandler(
    IIdentityRepository repository,
    IOtpHasher otpHasher,
    ILogger<VerifyEmailHandler> logger
) : IRequestHandler<VerifyEmailCommand, Result<VerifyEmailResponse>>
{
    private const int TokenExpiryMinutes = 15;

    public async Task<Result<VerifyEmailResponse>> Handle(
        VerifyEmailCommand request,
        CancellationToken cancellationToken)
    {
        var account = await repository.FindAccountByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result.Failure<VerifyEmailResponse>(Error.NotFound("resource", "Account not found."));

        if (account.Status != AccountStatus.PendingEmailVerification)
        {
            return Result.Failure<VerifyEmailResponse>(Error.Conflict(
                "domain", $"Email verification is not applicable in status {account.Status}."));
        }

        var otpRecord = await repository.FindActiveOtpAsync(
            request.AccountId, OtpPurpose.EmailVerify, cancellationToken);

        if (otpRecord is null)
        {
            return Result.Failure<VerifyEmailResponse>(Error.Validation(
                "token", "No active email verification token found. Please restart registration."));
        }

        if (otpRecord.IsExpired())
        {
            return Result.Failure<VerifyEmailResponse>(Error.Validation(
                "token", "The email verification link has expired. Please restart registration."));
        }

        if (otpRecord.AttemptCount >= 3)
        {
            return Result.Failure<VerifyEmailResponse>(Error.Validation(
                "token", "Too many attempts. Please restart registration."));
        }

        otpRecord.RecordAttempt();

        if (!otpHasher.Verify(request.Token, otpRecord.CodeHash))
        {
            await repository.SaveChangesAsync(cancellationToken);
            return Result.Failure<VerifyEmailResponse>(Error.Validation(
                "token", "Invalid verification link. Please request a new one."));
        }

        otpRecord.MarkUsed();
        account.MarkEmailVerified();

        var requests = await repository.GetRequestsByAccountAsync(request.AccountId, cancellationToken);
        var identityRequest = requests.Find(r => r.Status == RequestStatus.Pending);
        if (identityRequest is not null)
        {
            identityRequest.MarkUnderReview();
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[VerifyEmail] Email verified for AccountId={AccountId}",
            request.AccountId);

        return Result.Success(new VerifyEmailResponse(
            "Email verified successfully. Your account is now awaiting admin review."));
    }
}