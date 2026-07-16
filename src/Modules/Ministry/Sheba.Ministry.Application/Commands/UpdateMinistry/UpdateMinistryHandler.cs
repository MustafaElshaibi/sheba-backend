using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Ministry.Application.Commands.UpdateMinistry;

public sealed class UpdateMinistryHandler(
    IMinistryRepository repository,
    ILogger<UpdateMinistryHandler> logger
) : IRequestHandler<UpdateMinistryCommand, UpdateMinistryResponse>
{
    public async Task<UpdateMinistryResponse> Handle(
        UpdateMinistryCommand request, CancellationToken ct)
    {
        var ministry = await repository.GetByIdAsync(request.MinistryId, ct)
            ?? throw new NotFoundException("Ministry", request.MinistryId);

        ministry.Update(
            request.NameAr, request.NameEn,
            request.DescriptionAr, request.DescriptionEn,
            request.LogoUrl, request.WebsiteUrl,
            request.ContactEmail, request.ContactPhone,
            request.AddressAr, request.AddressEn,
            request.DisplayOrder);

        if (request.IsActive) ministry.Activate();
        else ministry.Deactivate();

        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[UpdateMinistry] Updated {Code} (Id={Id})", ministry.Code, ministry.Id);
        return new UpdateMinistryResponse(ministry.Id, "Ministry updated successfully.");
    }
}
