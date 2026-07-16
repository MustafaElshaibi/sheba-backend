using Sheba.Ministry.Domain.Enums;
using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Ministry.Domain.Entities;

/// <summary>
/// A specific API endpoint exposed by a ministry that Sheba can call.
/// </summary>
public sealed class MinistryEndpoint : BaseEntity
{
    public Guid MinistryId { get; private set; }
    public Guid? AuthConfigId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? DescriptionAr { get; private set; }
    public string? DescriptionEn { get; private set; }
    public string HttpMethod { get; private set; } = "GET";
    public string PathTemplate { get; private set; } = string.Empty;
    public string? RequestSchemaJson { get; private set; }
    public string? ResponseSchemaJson { get; private set; }
    public EndpointType Type { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int TimeoutSeconds { get; private set; } = 30;
    public int? RateLimitPerMinute { get; private set; }
    public bool RequiresCitizenConsent { get; private set; }

    // EF Core
    private MinistryEndpoint() { }

    public static MinistryEndpoint Create(
        Guid ministryId,
        string code,
        string nameAr,
        string nameEn,
        string httpMethod,
        string pathTemplate,
        EndpointType type,
        Guid? authConfigId = null,
        string? descriptionAr = null,
        string? descriptionEn = null,
        int timeoutSeconds = 30,
        int? rateLimitPerMinute = null,
        bool requiresCitizenConsent = false)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Endpoint code is required.");
        if (string.IsNullOrWhiteSpace(pathTemplate))
            throw new DomainException("Path template is required.");

        var method = httpMethod.Trim().ToUpperInvariant();
        if (method is not ("GET" or "POST" or "PUT" or "DELETE" or "PATCH"))
            throw new DomainException($"Invalid HTTP method: {httpMethod}");

        return new MinistryEndpoint
        {
            MinistryId = ministryId,
            AuthConfigId = authConfigId,
            Code = code.Trim(),
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            DescriptionAr = descriptionAr,
            DescriptionEn = descriptionEn,
            HttpMethod = method,
            PathTemplate = pathTemplate.Trim(),
            Type = type,
            TimeoutSeconds = timeoutSeconds,
            RateLimitPerMinute = rateLimitPerMinute,
            RequiresCitizenConsent = requiresCitizenConsent
        };
    }

    public void Update(
        string nameAr, string nameEn,
        string? descriptionAr, string? descriptionEn,
        string httpMethod, string pathTemplate,
        EndpointType type, Guid? authConfigId,
        int timeoutSeconds, int? rateLimitPerMinute,
        bool requiresCitizenConsent)
    {
        NameAr = nameAr; NameEn = nameEn;
        DescriptionAr = descriptionAr; DescriptionEn = descriptionEn;
        HttpMethod = httpMethod.Trim().ToUpperInvariant();
        PathTemplate = pathTemplate; Type = type;
        AuthConfigId = authConfigId;
        TimeoutSeconds = timeoutSeconds;
        RateLimitPerMinute = rateLimitPerMinute;
        RequiresCitizenConsent = requiresCitizenConsent;
        Touch();
    }

    public void Activate() { IsActive = true; Touch(); }
    public void Deactivate() { IsActive = false; Touch(); }

    public void SetSchemas(string? requestSchema, string? responseSchema)
    {
        RequestSchemaJson = requestSchema;
        ResponseSchemaJson = responseSchema;
        Touch();
    }
}
