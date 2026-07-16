using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Interfaces;

namespace Sheba.Ministry.Application.Queries.GetMinistryById;

public sealed class GetMinistryByIdHandler(
    IMinistryRepository repository,
    ILogger<GetMinistryByIdHandler> logger
) : IRequestHandler<GetMinistryByIdQuery, MinistryDetailDto?>
{
    public async Task<MinistryDetailDto?> Handle(
        GetMinistryByIdQuery request, CancellationToken ct)
    {
        var m = await repository.GetByIdAsync(request.MinistryId, ct);
        if (m is null)
        {
            logger.LogWarning("[GetMinistryById] Ministry {Id} not found", request.MinistryId);
            return null;
        }

        return new MinistryDetailDto(
            m.Id, m.Code, m.NameAr, m.NameEn,
            m.DescriptionAr, m.DescriptionEn,
            m.LogoUrl, m.WebsiteUrl,
            m.ContactEmail, m.ContactPhone,
            m.ParentMinistryId, m.DepthLevel, m.DisplayOrder,
            m.IsActive, m.CreatedAt, m.UpdatedAt,
            AuthConfigs: m.AuthConfigs.Select(c => new AuthConfigDto(
                c.Id, c.Name, c.AuthType, c.BaseUrl,
                c.IsActive, c.IsDefault, c.HealthCheckPath,
                c.TimeoutSeconds, c.RetryCount,
                c.Credential is not null,
                c.Credential?.LastVerifiedAt
            )).ToList(),
            Endpoints: m.Endpoints.Select(e => new EndpointDto(
                e.Id, e.Code, e.NameAr, e.NameEn,
                e.HttpMethod, e.PathTemplate, e.Type,
                e.IsActive, e.AuthConfigId
            )).ToList(),
            Webhooks: m.Webhooks.Select(w => new WebhookDto(
                w.Id, w.EventType, w.ShebaWebhookPath,
                w.IsActive, w.LastReceivedAt
            )).ToList());
    }
}
