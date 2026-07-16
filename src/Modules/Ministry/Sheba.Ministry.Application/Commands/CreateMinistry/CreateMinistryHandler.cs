using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Ministry.Application.Commands.CreateMinistry;

public sealed class CreateMinistryHandler(
    IMinistryRepository repository,
    ILogger<CreateMinistryHandler> logger
) : IRequestHandler<CreateMinistryCommand, CreateMinistryResponse>
{
    public async Task<CreateMinistryResponse> Handle(
        CreateMinistryCommand request, CancellationToken ct)
    {
        // Guard: code must be unique
        var existing = await repository.GetByCodeAsync(request.Code.Trim().ToUpperInvariant(), ct);
        if (existing is not null)
            throw new DomainException($"Ministry code '{request.Code}' is already in use.");

        int parentDepth = -1;
        if (request.ParentMinistryId.HasValue)
        {
            var parent = await repository.GetByIdAsync(request.ParentMinistryId.Value, ct)
                ?? throw new NotFoundException("Ministry", request.ParentMinistryId.Value);
            parentDepth = parent.DepthLevel;
        }

        var ministry = Domain.Entities.Ministry.Create(
            request.Code, request.NameAr, request.NameEn,
            request.ParentMinistryId, parentDepth);

        if (request.DescriptionAr is not null || request.DescriptionEn is not null ||
            request.ContactEmail is not null || request.ContactPhone is not null)
        {
            ministry.Update(
                request.NameAr, request.NameEn,
                request.DescriptionAr, request.DescriptionEn,
                null, null,
                request.ContactEmail, request.ContactPhone,
                null, null, 0);
        }

        await repository.AddAsync(ministry, ct);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[CreateMinistry] Created {Code} (Id={Id})", ministry.Code, ministry.Id);

        return new CreateMinistryResponse(ministry.Id, ministry.Code, ministry.NameEn);
    }
}
