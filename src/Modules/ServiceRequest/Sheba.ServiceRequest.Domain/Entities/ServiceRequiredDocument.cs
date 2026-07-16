using Sheba.Shared.Kernel.Entities;

namespace Sheba.ServiceRequest.Domain.Entities;

/// <summary>
/// A document type required for submitting a service request.
/// </summary>
public sealed class ServiceRequiredDocument : BaseEntity
{
    public Guid ServiceId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public bool IsMandatory { get; private set; } = true;
    public int MaxSizeMb { get; private set; } = 5;
    public string? AllowedMimeTypesCsv { get; private set; }    // comma-separated

    private ServiceRequiredDocument() { }

    public static ServiceRequiredDocument Create(
        Guid serviceId, string documentType, string nameAr, string nameEn,
        bool isMandatory = true, int maxSizeMb = 5, string? allowedMimeTypes = null)
    {
        return new ServiceRequiredDocument
        {
            ServiceId = serviceId,
            DocumentType = documentType.Trim().ToUpperInvariant(),
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            IsMandatory = isMandatory,
            MaxSizeMb = maxSizeMb,
            AllowedMimeTypesCsv = allowedMimeTypes
        };
    }
}
