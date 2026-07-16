using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Identity.Infrastructure.Persistence;

namespace Sheba.Identity.Infrastructure.Adapters;

/// <summary>
/// Mock civil registry adapter for development and testing.
/// Contains 8 seeded test citizens covering all registration scenarios from Section 24.
///
/// Active when: NationalId:ActiveProvider = "Mock" (default in Development).
/// Never use in production — switch to HttpNationalIdProvider.
/// </summary>
public sealed class MockNationalIdProvider(
    ILogger<MockNationalIdProvider> logger) : INationalIdProvider
{
    /// <summary>
    /// In-memory mock civil registry.
    /// Key = NationalId | value = record tuple.
    /// Format: (FullNameAr, FullNameEn, RegisteredPhone, Status, DOB, Gender)
    /// </summary>
    private static readonly Dictionary<string, (string NameAr, string NameEn, string Phone, NidStatus Status, DateOnly Dob, string Gender)>
        MockRegistry = new()
        {
            // ── Valid citizens (happy path) — phones exactly as documented in Section 24 ──
            ["1000000001"] = ("أحمد اليمني",    "Ahmed Al-Yemeni",   "0777000001", NidStatus.Valid,    new DateOnly(1990, 3, 15), "M"),
            ["1000000002"] = ("فاطمة الصنعاء", "Fatima Al-Sana'a",  "0777000002", NidStatus.Valid,    new DateOnly(1995, 7, 22), "F"),
            ["1000000003"] = ("عمر الحضرمي",   "Omar Al-Hadhrami",  "0777000003", NidStatus.Valid,    new DateOnly(1988, 11, 5), "M"),
            ["1000000004"] = ("سارة عدن",       "Sara Al-Aden",      "0777000004", NidStatus.Valid,    new DateOnly(1993, 2, 28), "F"),

            // ── Rejection scenarios ──────────────────────────────────────────
            ["1000000099"] = ("مواطن متوفى",    "Deceased Citizen",  "0777000099", NidStatus.Deceased,  new DateOnly(1960, 1, 1),  "M"),
            ["1000000098"] = ("مواطن موقوف",    "Suspended Citizen", "0777000098", NidStatus.Suspended, new DateOnly(1975, 6, 14), "M"),
            ["1000000097"] = ("هوية منتهية",    "Expired NID",       "0777000097", NidStatus.Expired,   new DateOnly(1980, 9, 3),  "M"),
            // Phone mismatch: NID 1000000096 is valid, but its registered phone is 0777000096.
            // The documented test enters phone 0777000001, which won't match → returns NotFound
            // (intentional: no information leakage about which check failed).
            ["1000000096"] = ("خطأ في الهاتف",  "Phone Mismatch",    "0777000096", NidStatus.Valid,    new DateOnly(1985, 4, 17), "M"),
        };

    public Task<NationalIdLookupResult> LookupAsync(
        string nationalId,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[MockNID] Lookup NID={NationalId} Phone={Phone}",
            nationalId, phoneNumber);

        // Normalize the submitted phone: strip country prefix, ensure +967 format
        var normalizedInput = NormalizePhone(phoneNumber);

        if (!MockRegistry.TryGetValue(nationalId, out var record))
        {
            logger.LogWarning("[MockNID] NID {NationalId} not found in mock registry", nationalId);
            return Task.FromResult(new NationalIdLookupResult(
                IsFound: false,
                NationalId: nationalId,
                FullNameAr: string.Empty,
                FullNameEn: string.Empty,
                PhoneNumber: string.Empty,
                DateOfBirth: default,
                Gender: string.Empty,
                Status: NidStatus.NotFound));
        }

        // Phone mismatch check
        var registeredPhone = NormalizePhone(record.Phone);
        if (!string.Equals(registeredPhone, normalizedInput, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "[MockNID] NID {NationalId} phone mismatch: expected {Expected}, got {Got}",
                nationalId, registeredPhone, normalizedInput);

            return Task.FromResult(new NationalIdLookupResult(
                IsFound: false,
                NationalId: nationalId,
                FullNameAr: string.Empty,
                FullNameEn: string.Empty,
                PhoneNumber: string.Empty,
                DateOfBirth: default,
                Gender: string.Empty,
                Status: NidStatus.NotFound)); // intentional: no leakage of reason
        }

        logger.LogInformation(
            "[MockNID] NID {NationalId} found: {NameEn} / Status={Status}",
            nationalId, record.NameEn, record.Status);

        return Task.FromResult(new NationalIdLookupResult(
            IsFound: true,
            NationalId: nationalId,
            FullNameAr: record.NameAr,
            FullNameEn: record.NameEn,
            PhoneNumber: record.Phone,
            DateOfBirth: record.Dob,
            Gender: record.Gender,
            Status: record.Status));
    }

    private static string NormalizePhone(string phone)
    {
        var trimmed = phone.Trim().Replace(" ", "").Replace("-", "");
        if (trimmed.StartsWith("+967")) return trimmed;
        if (trimmed.StartsWith("967"))  return "+" + trimmed;
        if (trimmed.StartsWith("0"))    return "+967" + trimmed[1..];
        return "+967" + trimmed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Startup seeding: seed mock_citizens table in Development
    // Called by IdentityModule's SeedAsync extension (idempotent — skip if exists)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the mock registry for use in startup seeding.
    /// </summary>
    public static IReadOnlyDictionary<string, (string NameAr, string NameEn, string Phone, NidStatus Status, DateOnly Dob, string Gender)>
        GetMockRegistry() => MockRegistry;
}
