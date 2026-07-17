using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sheba.Admin.Application.Analytics.GetKpiSummary;
using Sheba.Admin.Application.Analytics.GetRegistrationTrends;
using Sheba.Admin.Application.Analytics.GetServiceRequestTrends;
using Sheba.Admin.Infrastructure.Reports;

namespace Sheba.Admin.Infrastructure;

/// <summary>
/// Minimal API endpoint definitions for the Admin module.
/// All endpoints are grouped under /api/admin with the "Admin" Swagger tag.
/// </summary>
internal static class AdminEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        // ── Analytics KPIs ─────────────────────────────────────────────────────
        group.MapGet("/analytics/kpis", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetKpiSummaryQuery(), ct);
            return Results.Ok(result);
        })
        .WithName("GetKpiSummary")
        .WithSummary("Returns live platform KPIs for the admin dashboard")
        .Produces<KpiSummaryDto>(200);

        // ── Registration Trends ────────────────────────────────────────────────
        group.MapGet("/analytics/trends/registrations", async (
            int? days,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetRegistrationTrendsQuery(days ?? 30);
            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        })
        .WithName("GetRegistrationTrends")
        .WithSummary("Daily registration counts for charts (last N days)")
        .Produces<List<TrendPointDto>>(200);

        // ── Service Request Trends ─────────────────────────────────────────────
        group.MapGet("/analytics/trends/service-requests", async (
            int? days,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetServiceRequestTrendsQuery(days ?? 30);
            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        })
        .WithName("GetServiceRequestTrends")
        .WithSummary("Daily service request counts for charts (last N days)")
        .Produces<List<ServiceTrendPointDto>>(200);

        // ── Identity Requests Report (PDF or Excel) ────────────────────────────
        group.MapGet("/reports/identity-requests", async (
            DateOnly from,
            DateOnly to,
            string? format,
            PdfReportGenerator pdfGen,
            ExcelReportGenerator excelGen,
            CancellationToken ct) =>
        {
            // Default to PDF; support "excel" as an alternative
            if (string.Equals(format, "excel", StringComparison.OrdinalIgnoreCase))
            {
                var (bytes, _) = await excelGen.GenerateServiceRequestReportAsync(from, to, ct);
                var fileName = $"identity-requests-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx";
                return Results.File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            else
            {
                var (bytes, _) = await pdfGen.GenerateIdentityReportAsync(from, to, ct);
                var fileName = $"identity-requests-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf";
                return Results.File(bytes, "application/pdf", fileName);
            }
        })
        .WithName("GetIdentityRequestsReport")
        .WithSummary("Download identity requests report as PDF (default) or Excel")
        .Produces(200);

        // ── Service Requests Report (Excel) ────────────────────────────────────
        group.MapGet("/reports/service-requests", async (
            DateOnly from,
            DateOnly to,
            ExcelReportGenerator excelGen,
            CancellationToken ct) =>
        {
            var (bytes, _) = await excelGen.GenerateServiceRequestReportAsync(from, to, ct);
            var fileName = $"service-requests-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx";
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        })
        .WithName("GetServiceRequestsReport")
        .WithSummary("Download service requests report as Excel")
        .Produces(200);

        // ── Audit Export (CSV) — exports analytics CSV until Audit module is built ──
        group.MapGet("/audit/export", async (
            DateOnly from,
            DateOnly to,
            string? type,
            CsvReportGenerator csvGen,
            CancellationToken ct) =>
        {
            byte[] bytes;
            string fileName;

            if (string.Equals(type, "service-requests", StringComparison.OrdinalIgnoreCase))
            {
                (bytes, _) = await csvGen.GenerateServiceRequestCsvAsync(from, to, ct);
                fileName = $"service-requests-export-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";
            }
            else
            {
                // Default: registration analytics
                (bytes, _) = await csvGen.GenerateRegistrationCsvAsync(from, to, ct);
                fileName = $"registration-export-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";
            }

            return Results.File(bytes, "text/csv", fileName);
        })
        .WithName("ExportAuditCsv")
        .WithSummary("Export analytics data as CSV (registrations or service-requests)")
        .Produces(200);
    }
}
