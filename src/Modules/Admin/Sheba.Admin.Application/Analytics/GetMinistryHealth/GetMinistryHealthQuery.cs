using MediatR;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Admin.Application.Analytics.GetMinistryHealth;

/// <summary>
/// Returns the latest connectivity health for every active ministry auth config, as recorded by
/// the scheduled MinistryHealthSweepJob (Phase 2 roadmap: "ministry health dashboard").
/// </summary>
/// <param name="MinistryId">
/// T-AUTH-3: narrows to one ministry (a MinistryManager's claim); null returns every ministry
/// (SuperAdmin/Auditor).
/// </param>
public sealed record GetMinistryHealthQuery(Guid? MinistryId = null) : IRequest<IReadOnlyList<MinistryHealthSnapshot>>;
