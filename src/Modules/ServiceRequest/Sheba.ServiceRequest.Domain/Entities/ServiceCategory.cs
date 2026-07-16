using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Domain.Entities;

/// <summary>
/// Hierarchical service category (e.g. Education -> School Enrollment -> Transfer).
/// </summary>
public sealed class ServiceCategory : BaseEntity
{
    public Guid? ParentId { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? IconUrl { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private readonly List<ServiceDefinition> _services = [];
    public IReadOnlyCollection<ServiceDefinition> Services => _services.AsReadOnly();

    private ServiceCategory() { }

    public static ServiceCategory Create(string nameAr, string nameEn, Guid? parentId = null, string? iconUrl = null, int displayOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(nameAr)) throw new DomainException("Arabic name is required.");
        if (string.IsNullOrWhiteSpace(nameEn)) throw new DomainException("English name is required.");

        return new ServiceCategory
        {
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            ParentId = parentId,
            IconUrl = iconUrl,
            DisplayOrder = displayOrder
        };
    }

    public void Update(string nameAr, string nameEn, string? iconUrl, int displayOrder)
    {
        NameAr = nameAr.Trim(); NameEn = nameEn.Trim();
        IconUrl = iconUrl; DisplayOrder = displayOrder;
        Touch();
    }

    public void Activate() { IsActive = true; Touch(); }
    public void Deactivate() { IsActive = false; Touch(); }
}
