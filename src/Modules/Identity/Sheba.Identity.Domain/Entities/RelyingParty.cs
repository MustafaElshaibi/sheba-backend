using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Entities;

namespace Sheba.Identity.Domain.Entities;

public sealed class RelyingParty : BaseEntity
{
    public string ClientId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public string? Description { get; private set; }
    public string? LogoUrl { get; private set; }
    public RpClientType ClientType { get; private set; }
    public RpPartyType PartyType { get; private set; }
    public Guid? MinistryId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public string Status { get; private set; } = "ACTIVE";
    public DateTime RegisteredAt { get; private set; } = DateTime.UtcNow;
    public Guid RegisteredBy { get; private set; }
    public string? Metadata { get; private set; }

    private readonly List<RpRedirectUri> _redirectUris = [];
    private readonly List<RpScope> _scopes = [];

    public IReadOnlyList<RpRedirectUri> RedirectUris => _redirectUris.AsReadOnly();
    public IReadOnlyList<RpScope> Scopes => _scopes.AsReadOnly();

    private RelyingParty() { }

    public static RelyingParty Create(
        string clientId,
        string name,
        RpClientType clientType,
        RpPartyType partyType,
        Guid registeredBy,
        string? nameAr = null,
        string? description = null,
        string? logoUrl = null,
        Guid? ministryId = null,
        Guid? organizationId = null)
    {
        return new RelyingParty
        {
            ClientId = clientId,
            Name = name,
            NameAr = nameAr,
            Description = description,
            LogoUrl = logoUrl,
            ClientType = clientType,
            PartyType = partyType,
            MinistryId = ministryId,
            OrganizationId = organizationId,
            RegisteredBy = registeredBy
        };
    }

    public void AddRedirectUri(string uri, string uriType = "REDIRECT")
    {
        _redirectUris.Add(RpRedirectUri.Create(Id, uri, uriType));
    }

    public void AddScope(string scopeName)
    {
        _scopes.Add(RpScope.Create(Id, scopeName));
    }

    public void Deactivate()
    {
        Status = "INACTIVE";
        Touch();
    }

    public void Activate()
    {
        Status = "ACTIVE";
        Touch();
    }
}

public sealed class RpRedirectUri : BaseEntity
{
    public Guid RelyingPartyId { get; private set; }
    public string Uri { get; private set; } = string.Empty;
    public string UriType { get; private set; } = "REDIRECT";

    private RpRedirectUri() { }

    public static RpRedirectUri Create(Guid relyingPartyId, string uri, string uriType = "REDIRECT")
    {
        return new RpRedirectUri
        {
            RelyingPartyId = relyingPartyId,
            Uri = uri,
            UriType = uriType
        };
    }
}

public sealed class RpScope : BaseEntity
{
    public Guid RelyingPartyId { get; private set; }
    public string ScopeName { get; private set; } = string.Empty;

    private RpScope() { }

    public static RpScope Create(Guid relyingPartyId, string scopeName)
    {
        return new RpScope
        {
            RelyingPartyId = relyingPartyId,
            ScopeName = scopeName
        };
    }
}