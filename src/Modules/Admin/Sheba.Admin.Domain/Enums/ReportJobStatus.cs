namespace Sheba.Admin.Domain.Enums;

/// <summary>Lifecycle status of a report generation job.</summary>
public enum ReportJobStatus
{
    Queued = 1,
    Running = 2,
    Done = 3,
    Failed = 4
}
