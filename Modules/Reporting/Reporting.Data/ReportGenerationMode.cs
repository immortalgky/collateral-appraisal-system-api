namespace Reporting.Data;

/// <summary>
/// Controls whether a report is generated synchronously (inline, within the HTTP request)
/// or asynchronously (via a background job, with the caller polling for the result).
/// </summary>
public enum ReportGenerationMode
{
    Sync,
    Async
}
