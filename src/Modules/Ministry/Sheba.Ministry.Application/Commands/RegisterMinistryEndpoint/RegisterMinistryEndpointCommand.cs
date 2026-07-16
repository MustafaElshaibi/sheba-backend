using MediatR;
using Sheba.Ministry.Domain.Enums;

namespace Sheba.Ministry.Application.Commands.RegisterMinistryEndpoint;

public sealed record RegisterMinistryEndpointCommand(
    Guid MinistryId,
    string Code,
    string NameAr,
    string NameEn,
    string HttpMethod,
    string PathTemplate,
    EndpointType Type,
    Guid? AuthConfigId = null,
    string? DescriptionAr = null,
    string? DescriptionEn = null,
    int TimeoutSeconds = 30,
    int? RateLimitPerMinute = null,
    bool RequiresCitizenConsent = false
) : IRequest<RegisterMinistryEndpointResponse>;

public sealed record RegisterMinistryEndpointResponse(Guid EndpointId, string Code, string Message);
