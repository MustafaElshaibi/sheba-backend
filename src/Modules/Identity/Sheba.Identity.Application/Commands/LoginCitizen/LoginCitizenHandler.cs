using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.LoginCitizen;

/// <summary>
/// Handles login credential validation + OTP dispatch. The ordering here is deliberate and
/// security-critical (see §6.3 / STRIDE): the password is verified *before* anything
/// account-specific is disclosed, so an outsider who lacks the password cannot use the
/// response to learn whether an identifier exists or what state that account is in.
///
/// 1. Look up account by NID or username
/// 2. Unknown account → burn comparable Argon2id time, then fail generically (no timing oracle)
/// 3. Locked account → fail generically *before* checking the password (brute-force gate)
/// 4. Verify password (Argon2id); on failure record the attempt and fail generically
/// 5. Only now, with ownership proven, apply the Approved-status gate (BR-ON-10) — a status
///    message here reaches only a caller who already holds the password, so it is not a leak
/// 6. Invalidate old LOGIN OTPs, send fresh OTP; return masked phone for the UI
/// </summary>
public sealed class LoginCitizenHandler(
    IIdentityRepository repository,
    IOtpProvider otpProvider,
    IPasswordHasher passwordHasher,
    IOtpHasher otpHasher,
    ILogger<LoginCitizenHandler> logger
) : IRequestHandler<LoginCitizenCommand, Result<LoginCitizenResponse>>
{
    // One identical message for every "you don't get in" outcome that an unauthenticated
    // caller can reach: unknown identifier, wrong password, or locked account. Keeping them
    // byte-identical is what prevents login from becoming an account-existence oracle.
    private const string GenericCredentialError =
        "Invalid credentials. Please check your National ID/username and password.";

    // A valid Argon2id hash of a throwaway value, computed once and reused. When the account
    // doesn't exist we still run a verification against it so the response latency matches the
    // real-account path — otherwise the (expensive) Argon2id step would only run for existing
    // accounts and its absence would time-leak which identifiers are real.
    private static string? _dummyHash;

    public async Task<Result<LoginCitizenResponse>> Handle(
        LoginCitizenCommand request,
        CancellationToken cancellationToken)
    {
        // ── Step 1: Look up account ───────────────────────────────────────────
        var account = await repository.FindAccountByUsernameOrNidAsync(
            request.UsernameOrNid, cancellationToken);

        // ── Step 2: Unknown account — equalize timing, then generic failure ────
        if (account is null)
        {
            var dummy = _dummyHash ??= passwordHasher.Hash("sheba-login-timing-equalizer");
            _ = passwordHasher.Verify(request.Password, dummy); // constant-ish work; result ignored
            logger.LogWarning("[LoginCitizen] Account not found: {Input}", MaskInput(request.UsernameOrNid));
            return Fail(GenericCredentialError);
        }

        // ── Step 3: Lock gate — before password, generic message ──────────────
        // Short-circuit locked accounts up front so a locked-out attacker cannot keep probing
        // passwords. The message stays generic so "locked" never confirms the account exists.
        if (account.IsLocked())
        {
            logger.LogWarning("[LoginCitizen] Locked-account login attempt: AccountId={AccountId}", account.Id);
            return Fail(GenericCredentialError);
        }

        // ── Step 4: Password verification (Argon2id via IPasswordHasher) ────────
        if (!passwordHasher.Verify(request.Password, account.PasswordHash))
        {
            account.RecordFailedLogin();
            await repository.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "[LoginCitizen] Invalid password for AccountId={AccountId}, FailedCount={Count}",
                account.Id, account.FailedLoginCount);

            return Fail(GenericCredentialError);
        }

        // ── Step 5: Status gate — only after ownership is proven (BR-ON-10) ─────
        // The caller has demonstrated the password, so telling them *why* a non-Approved
        // account can't sign in is helpful, not an enumeration leak.
        var statusMessage = account.Status switch
        {
            AccountStatus.Approved                  => null, // OK to proceed
            AccountStatus.PendingVerification       => "Your phone number is not yet verified. Please complete registration.",
            AccountStatus.PendingEmailVerification  => "Your email is not yet verified. Please click the link we sent.",
            AccountStatus.PendingAdminApproval      => "Your account is pending admin approval. You will be notified by email.",
            AccountStatus.Rejected                  => "Your account was not approved. Please contact support.",
            AccountStatus.Suspended                 => "Your account is suspended. Please contact support.",
            AccountStatus.Deactivated               => "This account has been deactivated.",
            _                                       => "Account status is invalid. Please contact support."
        };

        if (statusMessage is not null)
        {
            logger.LogInformation(
                "[LoginCitizen] Login refused for non-approved AccountId={AccountId} Status={Status}",
                account.Id, account.Status);
            return Fail(statusMessage);
        }

        // ── Step 6: Invalidate old OTPs and send new one ──────────────────────
        await repository.InvalidatePreviousOtpsAsync(account.Id, OtpPurpose.Login, cancellationToken);

        var (otpResult, rawCode) = await otpProvider.SendAsync(
            destination: account.PhoneNumber,
            purpose:     OtpPurpose.Login,
            channel:     OtpChannel.Sms,
            cancellationToken: cancellationToken);

        if (!otpResult.Succeeded)
        {
            logger.LogError("[LoginCitizen] OTP send failed for AccountId={AccountId}: {Error}",
                account.Id, otpResult.ErrorMessage);
            return Fail("Could not send verification SMS. Please try again shortly.");
        }

        var otpRecord = Domain.Entities.OtpRecord.Create(
            accountId:  account.Id,
            purpose:    OtpPurpose.Login,
            channel:    OtpChannel.Sms,
            codeHash:   otpHasher.Hash(rawCode),
            ttlMinutes: 5);

        await repository.AddOtpRecordAsync(otpRecord, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[LoginCitizen] OTP dispatched for AccountId={AccountId}", account.Id);

        return Result.Success(new LoginCitizenResponse(
            AccountId:  account.Id,
            MaskedPhone: MaskPhone(account.PhoneNumber)));
    }

    private static Result<LoginCitizenResponse> Fail(string reason) =>
        Result.Failure<LoginCitizenResponse>(Error.Validation("credentials", reason));

    private static string MaskInput(string? input) =>
        !string.IsNullOrEmpty(input) && input.Length > 3 ? input[..3] + "***" : "***";

    private static string MaskPhone(string phone) =>
        phone.Length < 7 ? "***" : phone[..^6] + "***" + phone[^3..];
}
