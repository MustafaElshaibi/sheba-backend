using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Application.Commands.SetServiceFee;

public sealed class SetServiceFeeHandler(
    IServiceDefinitionRepository repository,
    ILogger<SetServiceFeeHandler> logger
) : IRequestHandler<SetServiceFeeCommand, SetServiceFeeResponse>
{
    public async Task<SetServiceFeeResponse> Handle(SetServiceFeeCommand request, CancellationToken ct)
    {
        _ = await repository.GetServiceByIdAsync(request.ServiceId, ct)
            ?? throw new NotFoundException("ServiceDefinition", request.ServiceId);

        var fee = ServiceFee.Create(
            request.ServiceId, request.FeeType,
            request.NameAr, request.NameEn,
            request.Amount, request.Currency, request.IsMandatory);

        await repository.AddFeeAsync(fee, ct);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[SetServiceFee] Added {Type} fee ({Amount} {Currency}) to Service {Id}",
            request.FeeType, request.Amount, request.Currency, request.ServiceId);

        return new SetServiceFeeResponse(fee.Id, "Fee added successfully.");
    }
}
