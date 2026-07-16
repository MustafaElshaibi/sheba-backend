using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sheba.Admin.Application.Interfaces;
using Sheba.Admin.Domain.Enums;
using Sheba.Admin.Domain.Entities;
using Sheba.Admin.Infrastructure.Reports;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Admin.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job: every Monday 07:00 UTC, generates an Identity Requests PDF
/// for the previous week (Mon–Sun) and emails it to the configured admin address.
///
/// Registered in AdminModule.cs:
///   RecurringJob.AddOrUpdate&lt;ScheduledReportsJob&gt;(
///       "weekly-identity-report",
///       job => job.GenerateAndEmailWeeklyReportAsync(ct),
///       Cron.Weekly(DayOfWeek.Monday, 7));
/// </summary>
public sealed class ScheduledReportsJob(
    PdfReportGenerator pdfGenerator,
    IAdminAnalyticsRepository analyticsRepo,
    IEmailService emailService,
    IConfiguration configuration,
    ILogger<ScheduledReportsJob> logger)
{
    /// <summary>
    /// Entry point called by Hangfire on schedule.
    /// </summary>
    public async Task GenerateAndEmailWeeklyReportAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Previous week: Monday to Sunday
        var to = today.AddDays(-((int)today.DayOfWeek == 0 ? 7 : (int)today.DayOfWeek));
        // If today is Monday (1), subtract 1 to get Sunday, then go back 6 more days to Monday
        var from = to.AddDays(-6);

        logger.LogInformation(
            "[ScheduledReports] Generating weekly identity report for {From} to {To}",
            from, to);

        // Create a tracking record
        var job = ReportJob.Create(
            ReportType.IdentityRequests,
            ReportFormat.Pdf,
            requestedBy: Guid.Empty, // system-generated
            filtersJson: $"{{\"from\":\"{from:yyyy-MM-dd}\",\"to\":\"{to:yyyy-MM-dd}\"}}");

        await analyticsRepo.AddReportJobAsync(job, ct);
        job.MarkRunning();
        await analyticsRepo.SaveChangesAsync(ct);

        try
        {
            var (bytes, rowCount) = await pdfGenerator.GenerateIdentityReportAsync(from, to, ct);
            var fileName = $"identity-requests-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf";

            job.MarkDone(bytes, fileName, rowCount);
            await analyticsRepo.SaveChangesAsync(ct);

            // Email the report
            var adminEmail = configuration["Admin:ReportEmail"] ?? "admin@sheba.dev";
            var adminName = configuration["Admin:ReportEmailName"] ?? "Sheba Admin";

            var sent = await emailService.SendAsync(
                toAddress: adminEmail,
                toName: adminName,
                subject: $"Sheba Weekly Identity Report — {from:yyyy-MM-dd} to {to:yyyy-MM-dd}",
                htmlBody: $"""
                    <h2>Weekly Identity Requests Report</h2>
                    <p>Period: <strong>{from:yyyy-MM-dd}</strong> to <strong>{to:yyyy-MM-dd}</strong></p>
                    <p>Total rows: <strong>{rowCount}</strong></p>
                    <p>The full PDF report has been generated and is available for download from the admin portal.</p>
                    <hr/>
                    <p style="font-size:12px;color:#888;">
                        Generated automatically by Sheba e-Government Platform at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC.
                    </p>
                    """,
                cancellationToken: ct);

            if (sent)
                logger.LogInformation(
                    "[ScheduledReports] Weekly report emailed to {Email} — {RowCount} rows, {Size} bytes",
                    adminEmail, rowCount, bytes.Length);
            else
                logger.LogWarning(
                    "[ScheduledReports] Report generated but email delivery to {Email} failed",
                    adminEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ScheduledReports] Weekly report generation failed");
            job.MarkFailed(ex.Message);
            await analyticsRepo.SaveChangesAsync(ct);
            throw; // Let Hangfire handle retry
        }
    }
}
