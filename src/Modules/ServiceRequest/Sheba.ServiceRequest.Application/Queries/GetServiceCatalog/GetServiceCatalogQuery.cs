using MediatR;

namespace Sheba.ServiceRequest.Application.Queries.GetServiceCatalog;

public sealed record GetServiceCatalogQuery(bool IncludeInactive = false)
    : IRequest<ServiceCatalogResponse>;

public sealed record ServiceCatalogResponse(List<CategoryDto> Categories);

public sealed record CategoryDto(
    Guid Id, string NameAr, string NameEn, string? IconUrl,
    int DisplayOrder, bool IsActive, List<ServiceSummaryDto> Services);

public sealed record ServiceSummaryDto(
    Guid Id, string Code, string NameAr, string NameEn,
    string? DescriptionEn, int RequiredLoa, bool IsOnline,
    int? AverageDays, bool IsActive, List<FeeSummaryDto> Fees);

public sealed record FeeSummaryDto(
    string FeeType, string NameEn, decimal Amount, string Currency, bool IsMandatory);
