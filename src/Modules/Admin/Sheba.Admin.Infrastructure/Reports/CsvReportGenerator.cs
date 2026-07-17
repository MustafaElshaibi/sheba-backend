using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sheba.Admin.Infrastructure.Persistence;

namespace Sheba.Admin.Infrastructure.Reports;

/// <summary>
/// Generates CSV exports using CsvHelper.
/// Outputs UTF-8 with BOM for Excel compatibility.
/// </summary>
public sealed class CsvReportGenerator(
    AdminDbContext db,
    ILogger<CsvReportGenerator> logger)
{
    /// <summary>
    /// Exports registration analytics as a CSV file for the given date range.
    /// Returns the CSV as a byte array (UTF-8 with BOM).
    /// </summary>
    public async Task<(byte[] Bytes, int RowCount)> GenerateRegistrationCsvAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var snapshots = await db.DailyRegistrationSnapshots
            .Where(s => s.Date >= from && s.Date <= to)
            .OrderBy(s => s.Date)
            .ToListAsync(ct);

        logger.LogInformation(
            "[CsvReport] Generating registration CSV for {From} to {To} — {Count} rows",
            from, to, snapshots.Count);

        using var ms = new MemoryStream();
        // BOM for Excel auto-detect of UTF-8
        await ms.WriteAsync(Encoding.UTF8.GetPreamble(), ct);

        await using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        // Header
        csv.WriteField("Date");
        csv.WriteField("Total Registrations");
        csv.WriteField("Approved");
        csv.WriteField("Rejected");
        csv.WriteField("Pending EOD");
        csv.WriteField("Avg Approval Hours");
        await csv.NextRecordAsync();

        // Data
        foreach (var snap in snapshots)
        {
            csv.WriteField(snap.Date.ToString("yyyy-MM-dd"));
            csv.WriteField(snap.TotalRegistrations);
            csv.WriteField(snap.Approved);
            csv.WriteField(snap.Rejected);
            csv.WriteField(snap.PendingEod);
            csv.WriteField(snap.AvgApprovalHours?.ToString("F1") ?? "");
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync(ct);
        return (ms.ToArray(), snapshots.Count);
    }

    /// <summary>
    /// Exports service request analytics as a CSV file for the given date range.
    /// <paramref name="ministryId"/> narrows to one ministry (T-AUTH-3); null returns all.
    /// </summary>
    public async Task<(byte[] Bytes, int RowCount)> GenerateServiceRequestCsvAsync(
        DateOnly from, DateOnly to, Guid? ministryId = null, CancellationToken ct = default)
    {
        var snapshots = await db.DailyServiceRequestSnapshots
            .Where(s => s.Date >= from && s.Date <= to)
            .Where(s => ministryId == null || s.MinistryId == ministryId)
            .OrderBy(s => s.Date)
            .ThenBy(s => s.ServiceId)
            .ToListAsync(ct);

        logger.LogInformation(
            "[CsvReport] Generating service request CSV for {From} to {To} — {Count} rows",
            from, to, snapshots.Count);

        using var ms = new MemoryStream();
        await ms.WriteAsync(Encoding.UTF8.GetPreamble(), ct);

        await using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        // Header
        csv.WriteField("Date");
        csv.WriteField("Service ID");
        csv.WriteField("Ministry ID");
        csv.WriteField("Submitted");
        csv.WriteField("Completed");
        csv.WriteField("Rejected");
        csv.WriteField("Cancelled");
        csv.WriteField("SLA Breached");
        csv.WriteField("Avg Completion Hours");
        await csv.NextRecordAsync();

        foreach (var snap in snapshots)
        {
            csv.WriteField(snap.Date.ToString("yyyy-MM-dd"));
            csv.WriteField(snap.ServiceId);
            csv.WriteField(snap.MinistryId);
            csv.WriteField(snap.Submitted);
            csv.WriteField(snap.Completed);
            csv.WriteField(snap.Rejected);
            csv.WriteField(snap.Cancelled);
            csv.WriteField(snap.SlaBreach);
            csv.WriteField(snap.AvgCompletionHours?.ToString("F1") ?? "");
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync(ct);
        return (ms.ToArray(), snapshots.Count);
    }
}
