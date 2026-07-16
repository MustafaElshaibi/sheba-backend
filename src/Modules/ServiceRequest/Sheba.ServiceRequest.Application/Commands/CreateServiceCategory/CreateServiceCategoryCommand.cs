using MediatR;

namespace Sheba.ServiceRequest.Application.Commands.CreateServiceCategory;

public sealed record CreateServiceCategoryCommand(
    string NameAr, string NameEn, Guid? ParentId = null, string? IconUrl = null, int DisplayOrder = 0
) : IRequest<CreateServiceCategoryResponse>;

public sealed record CreateServiceCategoryResponse(Guid CategoryId, string NameEn);
