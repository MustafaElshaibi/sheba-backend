using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Admin.Application.Analytics.GetMinistryHealth;

public sealed class GetMinistryHealthHandler(
    IMinistryHealthProvider ministryHealth,
    ILogger<GetMinistryHealthHandler> logger
) : IRequestHandler<GetMinistryHealthQuery, IReadOnlyList<MinistryHealthSnapshot>>
{
    public async Task<IReadOnlyList<MinistryHealthSnapshot>> Handle(
        GetMinistryHealthQuery request, CancellationToken ct)
    {
        var snapshots = await ministryHealth.GetHealthSnapshotsAsync(request.MinistryId, ct);

        logger.LogDebug(
            "[GetMinistryHealth] Returning {Count} snapshot(s) (MinistryId={MinistryId})",
            snapshots.Count, request.MinistryId);

        return snapshots;
    }
}
