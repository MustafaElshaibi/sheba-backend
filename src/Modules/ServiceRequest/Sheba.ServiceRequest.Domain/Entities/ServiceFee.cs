using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Domain.Entities;

/// <summary>
/// Fee schedule for a government service.
/// </summary>
public sealed class ServiceFee : BaseEntity
{
    public Guid ServiceId { get; private set; }
    public string FeeType { get; private set; } = string.Empty;      // BASE, EXPEDITE, DELIVERY
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "YER";
    public bool IsMandatory { get; private set; } = true;
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidUntil { get; private set; }

    private ServiceFee() { }

    public static ServiceFee Create(
        Guid serviceId,
        string feeType,
        string nameAr,
        string nameEn,
        decimal amount,
        string currency = "YER",
        bool isMandatory = true,
        DateOnly? validFrom = null,
        DateOnly? validUntil = null)
    {
        if (amount < 0) throw new DomainException("Fee amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(feeType)) throw new DomainException("Fee type is required.");

        return new ServiceFee
        {
            ServiceId = serviceId,
            FeeType = feeType.Trim().ToUpperInvariant(),
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant(),
            IsMandatory = isMandatory,
            ValidFrom = validFrom ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ValidUntil = validUntil
        };
    }

    public void Update(decimal amount, string currency, bool isMandatory, DateOnly? validUntil)
    {
        Amount = amount; Currency = currency; IsMandatory = isMandatory;
        ValidUntil = validUntil; Touch();
    }
}
