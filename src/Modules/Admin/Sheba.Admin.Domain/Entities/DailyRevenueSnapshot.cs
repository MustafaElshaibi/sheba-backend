using Sheba.Shared.Kernel.Entities;

namespace Sheba.Admin.Domain.Entities;

/// <summary>
/// Daily snapshot of completed-payment revenue, keyed by (date, currency) since amounts in
/// different currencies can't be summed. Populated by OnPaymentCompletedHandler (T-PAY-1) —
/// never written to by API command handlers. Maps to admin_data.analytics_revenue_daily.
/// </summary>
public sealed class DailyRevenueSnapshot : BaseEntity
{
    public DateOnly Date { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public int PaymentsCompleted { get; private set; }

    private DailyRevenueSnapshot() { }

    public static DailyRevenueSnapshot Create(DateOnly date, string currency) =>
        new()
        {
            Id = Guid.NewGuid(),
            Date = date,
            Currency = currency,
            TotalAmount = 0,
            PaymentsCompleted = 0
        };

    public void RecordPayment(decimal amount)
    {
        TotalAmount += amount;
        PaymentsCompleted++;
        Touch();
    }
}
