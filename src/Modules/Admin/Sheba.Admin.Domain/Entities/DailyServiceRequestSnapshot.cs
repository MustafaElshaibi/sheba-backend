using Sheba.Shared.Kernel.Entities;

namespace Sheba.Admin.Domain.Entities;

/// <summary>
/// Daily snapshot of service request analytics per service.
/// Populated asynchronously by MediatR event handlers.
/// Maps to admin_data.analytics_service_requests_daily.
/// </summary>
public sealed class DailyServiceRequestSnapshot : BaseEntity
{
    public DateOnly Date { get; private set; }
    public Guid ServiceId { get; private set; }
    public Guid MinistryId { get; private set; }
    public int Submitted { get; private set; }
    public int Completed { get; private set; }
    public int Rejected { get; private set; }
    public int Cancelled { get; private set; }
    public int SlaBreach { get; private set; }
    public decimal? AvgCompletionHours { get; private set; }

    private DailyServiceRequestSnapshot() { }

    /// <summary>Creates a new snapshot for the given date and service.</summary>
    public static DailyServiceRequestSnapshot Create(DateOnly date, Guid serviceId, Guid ministryId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Date = date,
            ServiceId = serviceId,
            MinistryId = ministryId,
            Submitted = 0,
            Completed = 0,
            Rejected = 0,
            Cancelled = 0,
            SlaBreach = 0,
            AvgCompletionHours = null
        };

    public void IncrementSubmitted()
    {
        Submitted++;
        Touch();
    }

    public void IncrementCompleted()
    {
        Completed++;
        Touch();
    }

    public void IncrementRejected()
    {
        Rejected++;
        Touch();
    }

    public void IncrementCancelled()
    {
        Cancelled++;
        Touch();
    }

    public void IncrementSlaBreach()
    {
        SlaBreach++;
        Touch();
    }

    public void SetAvgCompletionHours(decimal hours)
    {
        AvgCompletionHours = hours;
        Touch();
    }
}
