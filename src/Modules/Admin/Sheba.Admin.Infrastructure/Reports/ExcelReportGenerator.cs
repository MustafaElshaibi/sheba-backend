using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sheba.Admin.Infrastructure.Persistence;

namespace Sheba.Admin.Infrastructure.Reports;

/// <summary>
/// Generates Service Requests Excel reports using ClosedXML.
/// Produces a styled workbook with auto-fit columns, bold headers, and a summary row.
/// </summary>
public sealed class ExcelReportGenerator(
    AdminDbContext db,
    ILogger<ExcelReportGenerator> logger)
{
    /// <summary>
    /// Generates an Excel report of daily service request snapshots for the given date range.
    /// <paramref name="ministryId"/> narrows to one ministry (T-AUTH-3); null returns all.
    /// Returns the workbook as a byte array.
    /// </summary>
    public async Task<(byte[] Bytes, int RowCount)> GenerateServiceRequestReportAsync(
        DateOnly from, DateOnly to, Guid? ministryId = null, CancellationToken ct = default)
    {
        var snapshots = await db.DailyServiceRequestSnapshots
            .Where(s => s.Date >= from && s.Date <= to)
            .Where(s => ministryId == null || s.MinistryId == ministryId)
            .OrderBy(s => s.Date)
            .ThenBy(s => s.ServiceId)
            .ToListAsync(ct);

        logger.LogInformation(
            "[ExcelReport] Generating service request report for {From} to {To} — {Count} rows",
            from, to, snapshots.Count);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Service Requests");

        // ── Header row ──────────────────────────────────────────────
        var headers = new[]
        {
            "Date", "Service ID", "Ministry ID",
            "Submitted", "Completed", "Rejected", "Cancelled",
            "SLA Breached", "Avg Completion (h)"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // ── Data rows ───────────────────────────────────────────────
        var row = 2;
        foreach (var snap in snapshots)
        {
            ws.Cell(row, 1).Value = snap.Date.ToString("yyyy-MM-dd");
            ws.Cell(row, 2).Value = snap.ServiceId.ToString();
            ws.Cell(row, 3).Value = snap.MinistryId.ToString();
            ws.Cell(row, 4).Value = snap.Submitted;
            ws.Cell(row, 5).Value = snap.Completed;
            ws.Cell(row, 6).Value = snap.Rejected;
            ws.Cell(row, 7).Value = snap.Cancelled;
            ws.Cell(row, 8).Value = snap.SlaBreach;
            ws.Cell(row, 9).Value = snap.AvgCompletionHours?.ToString("F1") ?? "—";

            // Alternate row shading
            if (row % 2 == 0)
            {
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            row++;
        }

        // ── Summary row ─────────────────────────────────────────────
        var summaryRow = row;
        ws.Cell(summaryRow, 1).Value = "TOTAL";
        ws.Cell(summaryRow, 1).Style.Font.Bold = true;
        ws.Cell(summaryRow, 4).Value = snapshots.Sum(s => s.Submitted);
        ws.Cell(summaryRow, 5).Value = snapshots.Sum(s => s.Completed);
        ws.Cell(summaryRow, 6).Value = snapshots.Sum(s => s.Rejected);
        ws.Cell(summaryRow, 7).Value = snapshots.Sum(s => s.Cancelled);
        ws.Cell(summaryRow, 8).Value = snapshots.Sum(s => s.SlaBreach);

        var avgHours = snapshots
            .Where(s => s.AvgCompletionHours.HasValue)
            .Select(s => s.AvgCompletionHours!.Value)
            .DefaultIfEmpty(0)
            .Average();
        ws.Cell(summaryRow, 9).Value = avgHours.ToString("F1");

        ws.Range(summaryRow, 1, summaryRow, headers.Length).Style.Fill.BackgroundColor = XLColor.LightYellow;
        ws.Range(summaryRow, 1, summaryRow, headers.Length).Style.Font.Bold = true;

        // ── Auto-fit & freeze panes ─────────────────────────────────
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return (ms.ToArray(), snapshots.Count);
    }
}
