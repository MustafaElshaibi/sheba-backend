using Sheba.Shared.Kernel.Entities;

namespace Sheba.Admin.Domain.Entities;

/// <summary>
/// Daily snapshot of identity registration analytics.
/// Populated asynchronously by MediatR event handlers — never written to by API command handlers.
/// Maps to admin_data.analytics_identity_daily.
/// </summary>
public sealed class DailyRegistrationSnapshot : BaseEntity
{
    public DateOnly Date { get; private set; }
    public int TotalRegistrations { get; private set; }
    public int Approved { get; private set; }
    public int Rejected { get; private set; }
    public int PendingEod { get; private set; }
    public decimal? AvgApprovalHours { get; private set; }

    private DailyRegistrationSnapshot() { }

    /// <summary>Creates a new snapshot for the given date (all counters at zero).</summary>
    public static DailyRegistrationSnapshot Create(DateOnly date) =>
        new()
        {
            Id = Guid.NewGuid(),
            Date = date,
            TotalRegistrations = 0,
            Approved = 0,
            Rejected = 0,
            PendingEod = 0,
            AvgApprovalHours = null
        };

    public void IncrementRegistration()
    {
        TotalRegistrations++;
        Touch();
    }

    public void IncrementApproved()
    {
        Approved++;
        Touch();
    }

    public void IncrementRejected()
    {
        Rejected++;
        Touch();
    }

    public void SetPendingEod(int count)
    {
        PendingEod = count;
        Touch();
    }

    public void SetAvgApprovalHours(decimal hours)
    {
        AvgApprovalHours = hours;
        Touch();
    }
}
