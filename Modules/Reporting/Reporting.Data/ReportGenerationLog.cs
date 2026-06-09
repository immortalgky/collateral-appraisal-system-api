namespace Reporting.Data;

/// <summary>
/// Audit record of one report export (file generation): which report, format, who, when, how long
/// it took, how many rows, the file size, and success/error. Persisted to reporting.ReportGenerationLogs.
/// </summary>
public class ReportGenerationLog
{
    public Guid Id { get; private set; }
    public string ReportName { get; private set; } = default!;
    public string Format { get; private set; } = default!;
    public string? GeneratedBy { get; private set; }
    public DateTime GeneratedAt { get; private set; }
    public int DurationMs { get; private set; }
    public int RowCount { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }

    private ReportGenerationLog() { }

    public static ReportGenerationLog Create(
        string reportName,
        string format,
        string? generatedBy,
        DateTime generatedAt,
        int durationMs,
        int rowCount,
        long? fileSizeBytes,
        bool success,
        string? errorMessage)
    {
        return new ReportGenerationLog
        {
            Id = Guid.CreateVersion7(),
            ReportName = reportName,
            Format = format,
            GeneratedBy = generatedBy,
            GeneratedAt = generatedAt,
            DurationMs = durationMs,
            RowCount = rowCount,
            FileSizeBytes = fileSizeBytes,
            Success = success,
            ErrorMessage = errorMessage is { Length: > 2000 } ? errorMessage[..2000] : errorMessage,
        };
    }
}
