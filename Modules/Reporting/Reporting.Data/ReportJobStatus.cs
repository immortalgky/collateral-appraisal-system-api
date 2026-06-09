namespace Reporting.Data;

/// <summary>
/// Lifecycle status of an async report generation job persisted in reporting.ReportJobs.
/// Transitions: Pending → Running → Completed | Failed.
/// </summary>
public enum ReportJobStatus
{
    Pending,
    Running,
    Completed,
    Failed
}
