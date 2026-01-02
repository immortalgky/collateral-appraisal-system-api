namespace Workflow.Workflow.ImportExport;

/// <summary>
/// Result of a workflow export operation
/// </summary>
public class WorkflowExportResult
{
    /// <summary>
    /// Whether the export was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Exported workflow data
    /// </summary>
    public string? ExportedData { get; set; }

    /// <summary>
    /// Binary data for binary formats
    /// </summary>
    public byte[]? BinaryData { get; set; }

    /// <summary>
    /// Export format used
    /// </summary>
    public WorkflowExportFormat Format { get; set; }

    /// <summary>
    /// Size of exported data in bytes
    /// </summary>
    public long DataSizeBytes { get; set; }

    /// <summary>
    /// Compressed size (if compression was used)
    /// </summary>
    public long? CompressedSizeBytes { get; set; }

    /// <summary>
    /// Number of workflows exported
    /// </summary>
    public int WorkflowCount { get; set; }

    /// <summary>
    /// Export execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Content type for HTTP responses
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Suggested file name for download
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Errors that occurred during export
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Warnings generated during export
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Export metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Export statistics
    /// </summary>
    public WorkflowExportStatistics Statistics { get; set; } = new();

    /// <summary>
    /// When the export was completed
    /// </summary>
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Create a successful export result
    /// </summary>
    public static WorkflowExportResult Success(string data, WorkflowExportFormat format, TimeSpan executionTime)
    {
        return new WorkflowExportResult
        {
            IsSuccess = true,
            ExportedData = data,
            Format = format,
            DataSizeBytes = System.Text.Encoding.UTF8.GetByteCount(data),
            ExecutionTime = executionTime,
            ContentType = GetContentType(format),
            FileName = $"workflow_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}{GetFileExtension(format)}"
        };
    }

    /// <summary>
    /// Create a successful binary export result
    /// </summary>
    public static WorkflowExportResult Success(byte[] data, WorkflowExportFormat format, TimeSpan executionTime)
    {
        return new WorkflowExportResult
        {
            IsSuccess = true,
            BinaryData = data,
            Format = format,
            DataSizeBytes = data.Length,
            ExecutionTime = executionTime,
            ContentType = GetContentType(format),
            FileName = $"workflow_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}{GetFileExtension(format)}"
        };
    }

    /// <summary>
    /// Create a failed export result
    /// </summary>
    public static WorkflowExportResult Failure(string errorMessage)
    {
        return new WorkflowExportResult
        {
            IsSuccess = false,
            Errors = new List<string> { errorMessage }
        };
    }

    private static string GetContentType(WorkflowExportFormat format)
    {
        return format switch
        {
            WorkflowExportFormat.Json => "application/json",
            WorkflowExportFormat.Yaml => "application/x-yaml",
            WorkflowExportFormat.Xml => "application/xml",
            WorkflowExportFormat.Binary => "application/octet-stream",
            WorkflowExportFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            WorkflowExportFormat.Bpmn => "application/xml",
            WorkflowExportFormat.Package => "application/zip",
            _ => "application/octet-stream"
        };
    }

    private static string GetFileExtension(WorkflowExportFormat format)
    {
        return format switch
        {
            WorkflowExportFormat.Json => ".json",
            WorkflowExportFormat.Yaml => ".yaml",
            WorkflowExportFormat.Xml => ".xml",
            WorkflowExportFormat.Binary => ".bin",
            WorkflowExportFormat.Excel => ".xlsx",
            WorkflowExportFormat.Bpmn => ".bpmn",
            WorkflowExportFormat.Package => ".zip",
            _ => ".dat"
        };
    }
}

/// <summary>
/// Statistics about the export operation
/// </summary>
public class WorkflowExportStatistics
{
    public int TotalActivities { get; set; }
    public int TotalVariables { get; set; }
    public int TotalExpressions { get; set; }
    public int TotalExecutionHistory { get; set; }
    public Dictionary<string, int> ActivityTypeCount { get; set; } = new();
    public double CompressionRatio { get; set; }
}