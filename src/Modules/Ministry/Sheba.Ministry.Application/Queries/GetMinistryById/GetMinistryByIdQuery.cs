using MediatR;
using Sheba.Ministry.Domain.Enums;

namespace Sheba.Ministry.Application.Queries.GetMinistryById;

public sealed record GetMinistryByIdQuery(Guid MinistryId)
    : IRequest<MinistryDetailDto?>;

public sealed record MinistryDetailDto(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    string? LogoUrl,
    string? WebsiteUrl,
    string? ContactEmail,
    string? ContactPhone,
    Guid? ParentMinistryId,
    int DepthLevel,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<AuthConfigDto> AuthConfigs,
    List<EndpointDto> Endpoints,
    List<WebhookDto> Webhooks);

public sealed record AuthConfigDto(
    Guid Id,
    string Name,
    MinistryAuthType AuthType,
    string BaseUrl,
    bool IsActive,
    bool IsDefault,
    string? HealthCheckPath,
    int TimeoutSeconds,
    int RetryCount,
    bool HasCredentials,
    DateTime? LastVerifiedAt);

public sealed record EndpointDto(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn,
    string HttpMethod,
    string PathTemplate,
    EndpointType Type,
    bool IsActive,
    Guid? AuthConfigId);

public sealed record WebhookDto(
    Guid Id,
    string EventType,
    string ShebaWebhookPath,
    bool IsActive,
    DateTime? LastReceivedAt);
