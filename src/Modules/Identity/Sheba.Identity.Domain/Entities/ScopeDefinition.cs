using Sheba.Shared.Kernel.Entities;

namespace Sheba.Identity.Domain.Entities;

public sealed class ScopeDefinition : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? DisplayNameAr { get; private set; }
    public string? Description { get; private set; }
    public string[] Claims { get; private set; } = Array.Empty<string>();
    public bool IsSystem { get; private set; }
    public int RequiresLoa { get; private set; } = 1;

    private ScopeDefinition() { }

    public static ScopeDefinition Create(
        string name,
        string displayName,
        string[] claims,
        bool isSystem = false,
        int requiresLoa = 1,
        string? displayNameAr = null,
        string? description = null)
    {
        return new ScopeDefinition
        {
            Name = name,
            DisplayName = displayName,
            DisplayNameAr = displayNameAr,
            Description = description,
            Claims = claims,
            IsSystem = isSystem,
            RequiresLoa = requiresLoa
        };
    }
}