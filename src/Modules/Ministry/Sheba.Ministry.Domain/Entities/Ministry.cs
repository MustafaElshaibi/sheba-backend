using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Ministry.Domain.Entities;

/// <summary>
/// Core ministry entity. Supports recursive sub-ministry hierarchy.
/// Top-level ministries have ParentMinistryId = null and DepthLevel = 0.
/// </summary>
public sealed class Ministry : BaseEntity
{
    public Guid? ParentMinistryId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? DescriptionAr { get; private set; }
    public string? DescriptionEn { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? AddressAr { get; private set; }
    public string? AddressEn { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int DepthLevel { get; private set; }
    public int DisplayOrder { get; private set; }
    public string? MetadataJson { get; private set; }

    // EF Core navigation (read-only)
    private readonly List<MinistryAuthConfig> _authConfigs = [];
    public IReadOnlyCollection<MinistryAuthConfig> AuthConfigs => _authConfigs.AsReadOnly();

    private readonly List<MinistryEndpoint> _endpoints = [];
    public IReadOnlyCollection<MinistryEndpoint> Endpoints => _endpoints.AsReadOnly();

    private readonly List<MinistryWebhook> _webhooks = [];
    public IReadOnlyCollection<MinistryWebhook> Webhooks => _webhooks.AsReadOnly();

    // EF Core
    private Ministry() { }

    /// <summary>Creates a new top-level ministry or sub-ministry.</summary>
    public static Ministry Create(
        string code,
        string nameAr,
        string nameEn,
        Guid? parentMinistryId = null,
        int parentDepthLevel = -1,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Ministry code is required.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new DomainException("Arabic name is required.");
        if (string.IsNullOrWhiteSpace(nameEn))
            throw new DomainException("English name is required.");

        var ministry = new Ministry
        {
            Code = code.Trim().ToUpperInvariant(),
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            ParentMinistryId = parentMinistryId,
            DepthLevel = parentMinistryId.HasValue ? parentDepthLevel + 1 : 0
        };
        // Seeding needs deterministic ids matching the service catalog's hardcoded ministry GUIDs
        // (T-MIN-1) — everywhere else, the BaseEntity default (a fresh Guid) is used.
        if (id.HasValue)
            ministry.Id = id.Value;

        return ministry;
    }

    public void Update(
        string nameAr,
        string nameEn,
        string? descriptionAr,
        string? descriptionEn,
        string? logoUrl,
        string? websiteUrl,
        string? contactEmail,
        string? contactPhone,
        string? addressAr,
        string? addressEn,
        int displayOrder)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
        LogoUrl = logoUrl;
        WebsiteUrl = websiteUrl;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        AddressAr = addressAr;
        AddressEn = addressEn;
        DisplayOrder = displayOrder;
        Touch();
    }

    public void Activate() { IsActive = true; Touch(); }
    public void Deactivate() { IsActive = false; Touch(); }
}
