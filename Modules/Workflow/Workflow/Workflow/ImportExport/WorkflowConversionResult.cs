namespace Workflow.Workflow.ImportExport;

/// <summary>
/// Result of workflow format conversion
/// </summary>
public class WorkflowConversionResult
{
    /// <summary>
    /// Whether conversion was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Converted workflow data
    /// </summary>
    public string? ConvertedData { get; set; }

    /// <summary>
    /// Binary converted data for binary formats
    /// </summary>
    public byte[]? BinaryData { get; set; }

    /// <summary>
    /// Original format
    /// </summary>
    public WorkflowExportFormat FromFormat { get; set; }

    /// <summary>
    /// Target format
    /// </summary>
    public WorkflowExportFormat ToFormat { get; set; }

    /// <summary>
    /// Size of original data
    /// </summary>
    public long OriginalSizeBytes { get; set; }

    /// <summary>
    /// Size of converted data
    /// </summary>
    public long ConvertedSizeBytes { get; set; }

    /// <summary>
    /// Conversion execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Conversion errors
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Conversion warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Data lost during conversion
    /// </summary>
    public List<string> DataLoss { get; set; } = new();

    /// <summary>
    /// Conversion metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Quality score of the conversion (0-100)
    /// </summary>
    public double QualityScore { get; set; } = 100.0;

    /// <summary>
    /// Create a successful conversion result
    /// </summary>
    public static WorkflowConversionResult Success(
        string convertedData,
        WorkflowExportFormat fromFormat,
        WorkflowExportFormat toFormat,
        long originalSize,
        TimeSpan executionTime)
    {
        return new WorkflowConversionResult
        {
            IsSuccess = true,
            ConvertedData = convertedData,
            FromFormat = fromFormat,
            ToFormat = toFormat,
            OriginalSizeBytes = originalSize,
            ConvertedSizeBytes = System.Text.Encoding.UTF8.GetByteCount(convertedData),
            ExecutionTime = executionTime
        };
    }

    /// <summary>
    /// Create a failed conversion result
    /// </summary>
    public static WorkflowConversionResult Failure(
        string errorMessage,
        WorkflowExportFormat fromFormat,
        WorkflowExportFormat toFormat)
    {
        return new WorkflowConversionResult
        {
            IsSuccess = false,
            FromFormat = fromFormat,
            ToFormat = toFormat,
            Errors = new List<string> { errorMessage }
        };
    }
}