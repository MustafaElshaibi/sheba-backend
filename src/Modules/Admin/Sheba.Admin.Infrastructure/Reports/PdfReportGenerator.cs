using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sheba.Admin.Infrastructure.Persistence;

namespace Sheba.Admin.Infrastructure.Reports;

/// <summary>
/// Generates Identity Requests PDF reports using QuestPDF.
/// Produces a styled report with header, date range, summary stats, and a detailed table.
/// </summary>
public sealed class PdfReportGenerator(
    AdminDbContext db,
    ILogger<PdfReportGenerator> logger)
{
    /// <summary>
    /// Generates a PDF report of daily registration snapshots for the given date range.
    /// Returns the PDF as a byte array.
    /// </summary>
    public async Task<(byte[] Bytes, int RowCount)> GenerateIdentityReportAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var snapshots = await db.DailyRegistrationSnapshots
            .Where(s => s.Date >= from && s.Date <= to)
            .OrderBy(s => s.Date)
            .ToListAsync(ct);

        logger.LogInformation(
            "[PdfReport] Generating identity report for {From} to {To} — {Count} rows",
            from, to, snapshots.Count);

        var totalApproved = snapshots.Sum(s => s.Approved);
        var totalRejected = snapshots.Sum(s => s.Rejected);
        var totalRegistrations = snapshots.Sum(s => s.TotalRegistrations);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                // ── Header ──────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Text("Sheba e-Government Platform")
                        .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().Text("Identity Requests Report")
                        .FontSize(14).SemiBold().FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"Period: {from:yyyy-MM-dd} to {to:yyyy-MM-dd} | Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                });

                // ── Content ─────────────────────────────────────────
                page.Content().PaddingVertical(10).Column(col =>
                {
                    // Summary cards
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Background(Colors.Blue.Lighten5).Padding(10).Column(c =>
                        {
                            c.Item().Text("Total Registrations").FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(totalRegistrations.ToString("N0")).FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Background(Colors.Green.Lighten5).Padding(10).Column(c =>
                        {
                            c.Item().Text("Approved").FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(totalApproved.ToString("N0")).FontSize(20).Bold().FontColor(Colors.Green.Darken2);
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Background(Colors.Red.Lighten5).Padding(10).Column(c =>
                        {
                            c.Item().Text("Rejected").FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(totalRejected.ToString("N0")).FontSize(20).Bold().FontColor(Colors.Red.Darken2);
                        });
                    });

                    col.Item().PaddingVertical(10);

                    // Data table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2); // Date
                            columns.RelativeColumn(2); // Total Registrations
                            columns.RelativeColumn(1); // Approved
                            columns.RelativeColumn(1); // Rejected
                            columns.RelativeColumn(1); // Pending EOD
                            columns.RelativeColumn(2); // Avg Approval Hours
                        });

                        // Header row
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                .Text("Date").FontColor(Colors.White).SemiBold();
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                .Text("Registrations").FontColor(Colors.White).SemiBold();
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                .Text("Approved").FontColor(Colors.White).SemiBold();
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                .Text("Rejected").FontColor(Colors.White).SemiBold();
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                .Text("Pending EOD").FontColor(Colors.White).SemiBold();
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                .Text("Avg Approval (h)").FontColor(Colors.White).SemiBold();
                        });

                        // Data rows
                        var isAlternate = false;
                        foreach (var snap in snapshots)
                        {
                            var bg = isAlternate ? Colors.Grey.Lighten4 : Colors.White;

                            table.Cell().Background(bg).Padding(4).Text(snap.Date.ToString("yyyy-MM-dd"));
                            table.Cell().Background(bg).Padding(4).Text(snap.TotalRegistrations.ToString());
                            table.Cell().Background(bg).Padding(4)
                                .Text(snap.Approved.ToString()).FontColor(Colors.Green.Darken2);
                            table.Cell().Background(bg).Padding(4)
                                .Text(snap.Rejected.ToString()).FontColor(Colors.Red.Darken2);
                            table.Cell().Background(bg).Padding(4).Text(snap.PendingEod.ToString());
                            table.Cell().Background(bg).Padding(4)
                                .Text(snap.AvgApprovalHours?.ToString("F1") ?? "—");

                            isAlternate = !isAlternate;
                        }
                    });
                });

                // ── Footer ──────────────────────────────────────────
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" of ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            });
        });

        var bytes = document.GeneratePdf();
        return (bytes, snapshots.Count);
    }
}
