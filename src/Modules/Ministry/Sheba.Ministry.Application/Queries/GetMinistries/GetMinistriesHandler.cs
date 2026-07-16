using MediatR;
using Sheba.Ministry.Domain.Interfaces;

namespace Sheba.Ministry.Application.Queries.GetMinistries;

public sealed class GetMinistriesHandler(
    IMinistryRepository repository
) : IRequestHandler<GetMinistriesQuery, List<MinistrySummaryDto>>
{
    public async Task<List<MinistrySummaryDto>> Handle(
        GetMinistriesQuery request, CancellationToken ct)
    {
        var ministries = await repository.GetAllAsync(request.IncludeInactive, ct);

        return ministries.Select(m => new MinistrySummaryDto(
            m.Id, m.Code, m.NameAr, m.NameEn,
            m.ParentMinistryId, m.DepthLevel, m.IsActive,
            m.Endpoints.Count, m.AuthConfigs.Count,
            m.CreatedAt
        )).ToList();
    }
}
