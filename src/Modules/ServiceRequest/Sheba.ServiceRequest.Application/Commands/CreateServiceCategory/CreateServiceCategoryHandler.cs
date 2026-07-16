using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Interfaces;

namespace Sheba.ServiceRequest.Application.Commands.CreateServiceCategory;

public sealed class CreateServiceCategoryHandler(
    IServiceDefinitionRepository repository,
    ILogger<CreateServiceCategoryHandler> logger
) : IRequestHandler<CreateServiceCategoryCommand, CreateServiceCategoryResponse>
{
    public async Task<CreateServiceCategoryResponse> Handle(CreateServiceCategoryCommand request, CancellationToken ct)
    {
        var category = ServiceCategory.Create(request.NameAr, request.NameEn, request.ParentId, request.IconUrl, request.DisplayOrder);
        await repository.AddCategoryAsync(category, ct);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[CreateServiceCategory] Created {Name} (Id={Id})", category.NameEn, category.Id);
        return new CreateServiceCategoryResponse(category.Id, category.NameEn);
    }
}
