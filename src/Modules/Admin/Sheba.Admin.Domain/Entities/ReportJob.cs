using Sheba.Admin.Domain.Enums;
using Sheba.Shared.Kernel.Entities;

namespace Sheba.Admin.Domain.Entities;

/// <summary>
/// Tracks a report generation request.
/// Created by admin → executed by Hangfire or inline → stores the generated file bytes.
/// Maps to admin_data.report_jobs.
/// </summary>
public sealed class ReportJob : BaseEntity
{
    public ReportType ReportType { get; private set; }
    public ReportFormat Format { get; private set; }
    public ReportJobStatus Status { get; private set; } = ReportJobStatus.Queued;

    /// <summary>JSON-serialized filter parameters (from, to, etc.).</summary>
    public string? FiltersJson { get; private set; }

    public Guid RequestedBy { get; private set; }
    public string? HangfireJobId { get; private set; }

    /// <summary>Generated file content (stored in DB for simplicity in graduation project).</summary>
    public byte[]? FileBytes { get; private set; }
    public string? FileName { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public int? RowCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private ReportJob() { }

    /// <summary>Creates a new report generation job.</summary>
    public static ReportJob Create(
        ReportType reportType,
        ReportFormat format,
        Guid requestedBy,
        string? filtersJson = null)
    {
        return new ReportJob
        {
            Id = Guid.NewGuid(),
            ReportType = reportType,
            Format = format,
            Status = ReportJobStatus.Queued,
            RequestedBy = requestedBy,
            FiltersJson = filtersJson
        };
    }

    public void MarkRunning(string? hangfireJobId = null)
    {
        Status = ReportJobStatus.Running;
        StartedAt = DateTime.UtcNow;
        HangfireJobId = hangfireJobId;
        Touch();
    }

    public void MarkDone(byte[] fileBytes, string fileName, int rowCount)
    {
        Status = ReportJobStatus.Done;
        FileBytes = fileBytes;
        FileName = fileName;
        FileSizeBytes = fileBytes.Length;
        RowCount = rowCount;
        CompletedAt = DateTime.UtcNow;
        Touch();
    }

    public void MarkFailed(string errorMessage)
    {
        Status = ReportJobStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
        Touch();
    }
}
