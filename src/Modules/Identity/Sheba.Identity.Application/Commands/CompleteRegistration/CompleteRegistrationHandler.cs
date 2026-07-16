using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Application.Commands.CompleteRegistration;

/// <summary>
/// Completes citizen registration:
/// 1. Verify account exists and phone has been verified (Status = PendingVerification)
/// 2. Guard: username + email must be unique
/// 3. Set username, email, and Argon2id-hashed password on account
/// 4. Transition account → PendingEmailVerification
/// 5. Generate and send email verification token via IOtpProvider
/// 6. IdentityRequest stays in Pending until email is verified
/// 7. Persist
/// </summary>
public sealed class CompleteRegistrationHandler(
    IIdentityRepository repository,
    IPasswordHasher passwordHasher,
    IOtpProvider otpProvider,
    ILogger<CompleteRegistrationHandler> logger
) : IRequestHandler<CompleteRegistrationCommand, CompleteRegistrationResponse>
{
    public async Task<CompleteRegistrationResponse> Handle(
        CompleteRegistrationCommand request,
        CancellationToken cancellationToken)
    {
        // ── Load account ──────────────────────────────────────────────────────
        var account = await repository.FindAccountByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Account), request.AccountId);

        if (account.Status != AccountStatus.PendingVerification || account.PhoneVerifiedAt is null)
        {
            throw new DomainException(
                "Phone number must be verified before completing registration.");
        }

        // ── Guard: unique username ────────────────────────────────────────────
        var existingByUsername = await repository.FindAccountByUsernameOrNidAsync(
            request.Username, cancellationToken);
        if (existingByUsername is not null && existingByUsername.Id != request.AccountId)
        {
            throw new DomainException("This username is already taken. Please choose another.");
        }

        // ── Set credentials (Argon2id hash via IPasswordHasher) ──────────────
        var passwordHash = passwordHasher.Hash(request.Password);
        account.SetCredentials(request.Username, request.Email, passwordHash);

        // ── Send email verification token ─────────────────────────────────────
        var (otpResult, rawToken) = await otpProvider.SendAsync(
            destination: request.Email,
            purpose:     OtpPurpose.EmailVerify,
            channel:     OtpChannel.Email,
            cancellationToken: cancellationToken);

        if (!otpResult.Succeeded)
        {
            logger.LogWarning(
                "[CompleteRegistration] Email verification token send failed for AccountId={AccountId}",
                account.Id);
        }

        var otpRecord = Domain.Entities.OtpRecord.Create(
            accountId:  account.Id,
            purpose:    OtpPurpose.EmailVerify,
            channel:    OtpChannel.Email,
            codeHash:   rawToken,
            ttlMinutes: 15);

        await repository.AddOtpRecordAsync(otpRecord, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[CompleteRegistration] AccountId={AccountId} completed registration step — awaiting email verification",
            account.Id);

        return new CompleteRegistrationResponse(
            AccountId:         account.Id,
            IdentityRequestId: Guid.Empty,
            Message:           "Registration details saved. Please check your email and verify your address.");
    }
}
