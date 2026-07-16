using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Application.Commands.VerifyEmail;

public sealed class VerifyEmailHandler(
    IIdentityRepository repository,
    ILogger<VerifyEmailHandler> logger
) : IRequestHandler<VerifyEmailCommand, VerifyEmailResponse>
{
    private const int TokenExpiryMinutes = 15;

    public async Task<VerifyEmailResponse> Handle(
        VerifyEmailCommand request,
        CancellationToken cancellationToken)
    {
        var account = await repository.FindAccountByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.AccountId);

        if (account.Status != AccountStatus.PendingEmailVerification)
        {
            return new VerifyEmailResponse(false,
                $"Email verification is not applicable in status {account.Status}.");
        }

        var otpRecord = await repository.FindActiveOtpAsync(
            request.AccountId, OtpPurpose.EmailVerify, cancellationToken);

        if (otpRecord is null)
        {
            return new VerifyEmailResponse(false,
                "No active email verification token found. Please restart registration.");
        }

        if (otpRecord.IsExpired())
        {
            return new VerifyEmailResponse(false,
                "The email verification link has expired. Please restart registration.");
        }

        if (otpRecord.AttemptCount >= 3)
        {
            return new VerifyEmailResponse(false,
                "Too many attempts. Please restart registration.");
        }

        otpRecord.RecordAttempt();

        if (otpRecord.CodeHash != request.Token)
        {
            await repository.SaveChangesAsync(cancellationToken);
            return new VerifyEmailResponse(false,
                "Invalid verification link. Please request a new one.");
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

        return new VerifyEmailResponse(true,
            "Email verified successfully. Your account is now awaiting admin review.");
    }
}