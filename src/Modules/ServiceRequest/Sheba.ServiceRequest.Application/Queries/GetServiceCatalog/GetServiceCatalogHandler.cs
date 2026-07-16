using MediatR;
using Sheba.ServiceRequest.Domain.Interfaces;

namespace Sheba.ServiceRequest.Application.Queries.GetServiceCatalog;

public sealed class GetServiceCatalogHandler(
    IServiceDefinitionRepository repository
) : IRequestHandler<GetServiceCatalogQuery, ServiceCatalogResponse>
{
    public async Task<ServiceCatalogResponse> Handle(GetServiceCatalogQuery request, CancellationToken ct)
    {
        var categories = await repository.GetAllCategoriesAsync(request.IncludeInactive, ct);

        var dtos = categories.Select(c => new CategoryDto(
            c.Id, c.NameAr, c.NameEn, c.IconUrl,
            c.DisplayOrder, c.IsActive,
            c.Services
                .Where(s => request.IncludeInactive || s.IsActive)
                .OrderBy(s => s.DisplayOrder)
                .Select(s => new ServiceSummaryDto(
                    s.Id, s.Code, s.NameAr, s.NameEn, s.DescriptionEn,
                    s.RequiredLoa, s.IsOnline, s.AverageDays, s.IsActive,
                    s.Fees.Select(f => new FeeSummaryDto(
                        f.FeeType, f.NameEn, f.Amount, f.Currency, f.IsMandatory
                    )).ToList()
                )).ToList()
        )).ToList();

        return new ServiceCatalogResponse(dtos);
    }
}
