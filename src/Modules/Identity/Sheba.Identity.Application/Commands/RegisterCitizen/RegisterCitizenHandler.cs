using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.RegisterCitizen;

/// <summary>
/// Handles Step 1 of citizen registration:
///
/// 1. Validate NID + phone via INationalIdProvider (civil registry lookup)
/// 2. Guard: reject if NID is not Valid (deceased, suspended, expired, phone mismatch, not found)
/// 3. Guard: reject if NID is already registered in Sheba
/// 4. Create Account (PendingVerification) + IdentityRequest (OPEN_ACCOUNT)
/// 5. Hash + store OTP record via IOtpProvider
/// 6. Persist everything in one transaction
/// 7. Publish domain events (picked up by MediatR dispatch after SaveChanges)
///
/// Returns: AccountId + masked phone number for the UI.
/// </summary>
public sealed class RegisterCitizenHandler(
    IIdentityRepository repository,
    INationalIdProvider nidProvider,
    IOtpProvider otpProvider,
    IOtpHasher otpHasher,
    IOtpCodeGenerator otpCodeGenerator,
    ILogger<RegisterCitizenHandler> logger
) : IRequestHandler<RegisterCitizenCommand, Result<RegisterCitizenResponse>>
{
    // BR-ON-3: every registration-check failure — not found, deceased, suspended, expired,
    // phone mismatch, AND already-registered — must return this single, identical message.
    // Distinguishing them turns registration into an oracle for "does NID X hold a Sheba
    // account / exist in the registry" (the enumeration attack GOV.UK One Login and UAE Pass
    // deliberately avoid). Specific reasons are logged/audited internally only.
    private const string GenericVerificationError =
        "We could not verify your identity. Please check your National ID and phone number and try again.";

    public async Task<Result<RegisterCitizenResponse>> Handle(
        RegisterCitizenCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[RegisterCitizen] Starting NID validation for NID={NationalId}",
            MaskNid(request.NationalId));

        // ── Step 1: Civil registry lookup ──────────────────────────────────────
        var nidResult = await nidProvider.LookupAsync(
            request.NationalId,
            request.PhoneNumber,
            cancellationToken);

        // Generic error on any NID failure — no information leakage
        if (!nidResult.IsFound || nidResult.Status != Domain.Interfaces.NidStatus.Valid)
        {
            logger.LogWarning(
                "[RegisterCitizen] NID check failed: NID={Nid} Status={Status}",
                MaskNid(request.NationalId), nidResult.Status);

            // Return the same error regardless of actual reason (no info leakage)
            return Result.Failure<RegisterCitizenResponse>(Error.Conflict("domain", GenericVerificationError));
        }

        // ── Step 2: Guard — already registered ─────────────────────────────────
        var existing = await repository.FindAccountByNidAsync(request.NationalId, cancellationToken);
        if (existing is not null)
        {
            logger.LogWarning(
                "[RegisterCitizen] NID {Nid} is already registered (AccountId={AccountId})",
                MaskNid(request.NationalId), existing.Id);

            // Same generic error as the NID-check failure above — see GenericVerificationError.
            // "Already registered" must not be distinguishable from "not in registry", or an
            // attacker learns which NIDs already hold Sheba accounts. Account recovery is a
            // separate, deliberately-initiated flow, not something we hint at here.
            return Result.Failure<RegisterCitizenResponse>(Error.Conflict("domain", GenericVerificationError));
        }

        // ── Step 3: Create Account entity ───────────────────────────────────────
        var account = Account.CreateFromNidCheck(
            nationalId:  request.NationalId,
            phoneNumber: nidResult.PhoneNumber,
            fullNameAr:  nidResult.FullNameAr,
            fullNameEn:  nidResult.FullNameEn);

        // ── Step 4: Create IdentityRequest ──────────────────────────────────────
        var citizenSnapshot = new
        {
            NationalId  = request.NationalId,
            FullNameAr  = nidResult.FullNameAr,
            FullNameEn  = nidResult.FullNameEn,
            PhoneNumber = nidResult.PhoneNumber,
            DateOfBirth = nidResult.DateOfBirth,
            Gender      = nidResult.Gender,
            CheckedAt   = DateTime.UtcNow
        };

        var identityRequest = IdentityRequest.Submit(
            accountId:       account.Id,
            requestType:     RequestType.OpenAccount,
            citizenSnapshot: citizenSnapshot);

        // ── Step 5: Generate and store OTP ──────────────────────────────────────
        var rawCode = otpCodeGenerator.GenerateNumericCode();
        var otpResult = await otpProvider.SendAsync(
            destination: nidResult.PhoneNumber,
            code:        rawCode,
            purpose:     OtpPurpose.Registration,
            channel:     OtpChannel.Sms,
            cancellationToken: cancellationToken);

        if (!otpResult.Succeeded)
        {
            logger.LogError(
                "[RegisterCitizen] OTP send failed for AccountId={AccountId}: {Error}",
                account.Id, otpResult.ErrorMessage);

            return Result.Failure<RegisterCitizenResponse>(Error.Conflict(
                "domain", "Could not send verification SMS. Please try again in a few minutes."));
        }

        // Hash the raw OTP code (Argon2id) — never store plaintext
        var codeHash = otpHasher.Hash(rawCode);

        var otpRecord = OtpRecord.Create(
            accountId:  account.Id,
            purpose:    OtpPurpose.Registration,
            channel:    OtpChannel.Sms,
            codeHash:   codeHash,
            ttlMinutes: 5);

        // ── Step 6: Persist in one atomic transaction ────────────────────────────
        await repository.AddAccountAsync(account, cancellationToken);
        await repository.AddIdentityRequestAsync(identityRequest, cancellationToken);
        await repository.AddOtpRecordAsync(otpRecord, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[RegisterCitizen] Account created. AccountId={AccountId} RequestId={RequestId}",
            account.Id, identityRequest.Id);

        return Result.Success(new RegisterCitizenResponse(
            AccountId:   account.Id,
            MaskedPhone: MaskPhone(nidResult.PhoneNumber)));
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static string MaskNid(string nid) =>
        nid.Length > 4
            ? string.Concat(new string('*', nid.Length - 4), nid[^4..])
            : "****";

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 7) return "***";
        return phone[..^6] + "***" + phone[^3..];
    }
}
