using MediatR;

namespace Sheba.Ministry.Application.Queries.GetMinistries;

public sealed record GetMinistriesQuery(bool IncludeInactive = false)
    : IRequest<List<MinistrySummaryDto>>;

public sealed record MinistrySummaryDto(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn,
    Guid? ParentMinistryId,
    int DepthLevel,
    bool IsActive,
    int EndpointCount,
    int AuthConfigCount,
    DateTime CreatedAt);
