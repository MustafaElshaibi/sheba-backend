using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Citizen.Application.Interfaces;
using Sheba.Citizen.Domain.Entities;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Citizen.Application.EventHandlers;

/// <summary>
/// Listens for IdentityRequestDecidedEvent. When an identity request is APPROVED, materializes
/// the citizen's CitizenProfile (sheba.md §5.2) — the row other modules read via
/// ICitizenAccountQueryService (T-CIT-1).
///
/// Guarded two ways against at-least-once outbox redelivery: IInboxGuard (T-EVT-1) and a
/// belt-and-suspenders existence check, since CitizenProfile.AccountId also has a unique DB index.
/// </summary>
public sealed class CreateCitizenProfileOnApprovalHandler(
    ICitizenProfileRepository profiles,
    ICitizenAccountQueryService accountQuery,
    IInboxGuard inboxGuard,
    ILogger<CreateCitizenProfileOnApprovalHandler> logger
) : INotificationHandler<IdentityRequestDecidedEvent>
{
    private const string ConsumerName = nameof(CreateCitizenProfileOnApprovalHandler);

    public async Task Handle(IdentityRequestDecidedEvent notification, CancellationToken ct)
    {
        if (!notification.Approved)
            return;

        if (await inboxGuard.IsProcessedAsync(notification.EventId, ConsumerName, ct))
            return;

        if (await profiles.GetByAccountIdAsync(notification.AccountId, ct) is not null)
        {
            await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, ct);
            return;
        }

        // NOTE: this queries Identity's implementation of ICitizenAccountQueryService (the only
        // one registered in DI today — see CitizenQueryAdapter's doc comment for the intended
        // long-term ownership swap, out of scope here).
        var account = await accountQuery.GetAccountInfoAsync(notification.AccountId, ct);
        if (account is null)
        {
            logger.LogWarning(
                "[CreateCitizenProfile] Account {AccountId} not found for approved request {RequestId}",
                notification.AccountId, notification.RequestId);
            return;
        }

        var profile = CitizenProfile.Create(
            accountId:   account.AccountId,
            nationalId:  account.NationalId,
            fullNameAr:  account.FullNameAr,
            fullNameEn:  account.FullNameEn,
            email:       account.Email);

        await profiles.AddAsync(profile, ct);
        await profiles.SaveChangesAsync(ct);
        await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, ct);

        logger.LogInformation(
            "[CreateCitizenProfile] Created profile {ProfileId} for approved account {AccountId}",
            profile.Id, account.AccountId);
    }
}
