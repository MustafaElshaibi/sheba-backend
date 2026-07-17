using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.RequestLoaUpgrade;

/// <summary>
/// Citizen requests an upgrade of their Level of Assurance (LoA 1 → 2 → 3).
/// Creates a new IdentityRequest (UpgradeLoa2 / UpgradeLoa3) that enters the admin
/// review queue exactly like an initial account request. KYC document attachment is
/// handled separately by the Document module (Week 8); this command opens the request.
///
/// API: POST /api/identity/loa/upgrade
/// Body: { accountId, targetLevel }   // targetLevel = 2 or 3
/// </summary>
public sealed record RequestLoaUpgradeCommand(
    Guid AccountId,
    int  TargetLevel
) : IRequest<Result<RequestLoaUpgradeResponse>>;

public sealed record RequestLoaUpgradeResponse(
    Guid   IdentityRequestId,
    int    TargetLevel,
    string Message
);
