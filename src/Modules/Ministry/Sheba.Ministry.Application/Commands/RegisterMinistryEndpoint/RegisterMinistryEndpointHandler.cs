using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Ministry.Application.Commands.RegisterMinistryEndpoint;

public sealed class RegisterMinistryEndpointHandler(
    IMinistryRepository repository,
    ILogger<RegisterMinistryEndpointHandler> logger
) : IRequestHandler<RegisterMinistryEndpointCommand, RegisterMinistryEndpointResponse>
{
    public async Task<RegisterMinistryEndpointResponse> Handle(
        RegisterMinistryEndpointCommand request, CancellationToken ct)
    {
        _ = await repository.GetByIdAsync(request.MinistryId, ct)
            ?? throw new NotFoundException("Ministry", request.MinistryId);

        var endpoint = MinistryEndpoint.Create(
            request.MinistryId, request.Code, request.NameAr, request.NameEn,
            request.HttpMethod, request.PathTemplate, request.Type,
            request.AuthConfigId, request.DescriptionAr, request.DescriptionEn,
            request.TimeoutSeconds, request.RateLimitPerMinute, request.RequiresCitizenConsent);

        await repository.AddEndpointAsync(endpoint, ct);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[RegisterEndpoint] Created endpoint {Code} for Ministry {Id}",
            endpoint.Code, request.MinistryId);

        return new RegisterMinistryEndpointResponse(endpoint.Id, endpoint.Code, "Endpoint registered.");
    }
}
