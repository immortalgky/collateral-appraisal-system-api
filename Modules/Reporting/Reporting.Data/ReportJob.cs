namespace Reporting.Data;

/// <summary>
/// Tracks one async PDF generation job dispatched to Hangfire.
/// State machine: Pending → Running → Completed | Failed.
///
/// Persisted to reporting.ReportJobs. Created by the enqueue endpoint and mutated
/// exclusively by ReportGenerationJob (the Hangfire worker).
/// </summary>
public class ReportJob
{
    public Guid Id { get; private set; }

    /// <summary>Matches IReportDataProvider.ReportTypeKey / ReportDefinition.ReportTypeKey.</summary>
    public string ReportTypeKey { get; private set; } = default!;

    /// <summary>The domain entity identifier passed to the data provider (e.g. appraisalId).</summary>
    public string EntityId { get; private set; } = default!;

    /// <summary>Current lifecycle status, stored as string.</summary>
    public ReportJobStatus Status { get; private set; }

    /// <summary>Bank code (UserCode) of the user who requested the report. Never a Guid.</summary>
    public string RequestedBy { get; private set; } = default!;

    public DateTime RequestedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <summary>Absolute path to the generated PDF on disk. Null until the job completes.</summary>
    public string? StoragePath { get; private set; }

    /// <summary>Suggested download filename (e.g. "appraisal-summary-land-building.pdf").</summary>
    public string? FileName { get; private set; }

    public long? FileSizeBytes { get; private set; }

    /// <summary>Wall-clock milliseconds from job start to PDF written to disk.</summary>
    public int? DurationMs { get; private set; }

    /// <summary>Error detail when Status == Failed. Truncated to 2 000 characters.</summary>
    public string? ErrorMessage { get; private set; }

    private ReportJob() { }

    /// <summary>
    /// Create a new job in Pending state. Called by the enqueue endpoint.
    /// </summary>
    public static ReportJob Create(
        string reportTypeKey,
        string entityId,
        string requestedBy,
        DateTime requestedAt)
    {
        return new ReportJob
        {
            Id = Guid.CreateVersion7(),
            ReportTypeKey = reportTypeKey,
            EntityId = entityId,
            Status = ReportJobStatus.Pending,
            RequestedBy = requestedBy,
            RequestedAt = requestedAt,
        };
    }

    /// <summary>Transition Pending → Running. Called when the Hangfire worker picks up the job.</summary>
    public void MarkRunning(DateTime now)
    {
        Status = ReportJobStatus.Running;
        StartedAt = now;
    }

    /// <summary>Transition Running → Completed. Called after the PDF is written to disk.</summary>
    public void MarkCompleted(
        DateTime now,
        string storagePath,
        string fileName,
        long fileSizeBytes,
        int durationMs)
    {
        Status = ReportJobStatus.Completed;
        CompletedAt = now;
        StoragePath = storagePath;
        FileName = fileName;
        FileSizeBytes = fileSizeBytes;
        DurationMs = durationMs;
    }

    /// <summary>
    /// Transition Running → Failed. Error message is truncated to 2 000 characters.
    /// Called on any exception during generation, then rethrown so Hangfire records the failure.
    /// </summary>
    public void MarkFailed(DateTime now, string? error)
    {
        Status = ReportJobStatus.Failed;
        CompletedAt = now;
        ErrorMessage = error is { Length: > 2000 } ? error[..2000] : error;
    }
}
