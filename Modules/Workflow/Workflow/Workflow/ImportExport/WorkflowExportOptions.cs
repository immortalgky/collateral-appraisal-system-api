namespace Workflow.Workflow.ImportExport;

/// <summary>
/// Options for workflow export operations
/// </summary>
public class WorkflowExportOptions
{
    /// <summary>
    /// Include activity execution history
    /// </summary>
    public bool IncludeExecutionHistory { get; set; } = false;

    /// <summary>
    /// Include workflow variables and their values
    /// </summary>
    public bool IncludeVariables { get; set; } = true;

    /// <summary>
    /// Include workflow metadata
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    /// Include version history
    /// </summary>
    public bool IncludeVersionHistory { get; set; } = false;

    /// <summary>
    /// Include sensitive data (passwords, tokens, etc.)
    /// </summary>
    public bool IncludeSensitiveData { get; set; } = false;

    /// <summary>
    /// Compress exported data
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Compression level to use
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

    /// <summary>
    /// Encrypt exported data
    /// </summary>
    public bool EnableEncryption { get; set; } = false;

    /// <summary>
    /// Encryption key (if encryption enabled)
    /// </summary>
    public string? EncryptionKey { get; set; }

    /// <summary>
    /// Maximum file size for export (in MB)
    /// </summary>
    public int MaxFileSizeMB { get; set; } = 100;

    /// <summary>
    /// Include only active/published versions
    /// </summary>
    public bool ActiveVersionsOnly { get; set; } = true;

    /// <summary>
    /// Date range filter for execution history
    /// </summary>
    public DateTimeOffset? FromDate { get; set; }

    /// <summary>
    /// Date range filter for execution history
    /// </summary>
    public DateTimeOffset? ToDate { get; set; }

    /// <summary>
    /// Custom export filters
    /// </summary>
    public Dictionary<string, object> CustomFilters { get; set; } = new();

    /// <summary>
    /// Output file name (without extension)
    /// </summary>
    public string? OutputFileName { get; set; }

    /// <summary>
    /// Validate options before export
    /// </summary>
    public void Validate()
    {
        if (EnableEncryption && string.IsNullOrEmpty(EncryptionKey))
        {
            throw new InvalidOperationException("Encryption key is required when encryption is enabled");
        }

        if (MaxFileSizeMB <= 0)
        {
            throw new InvalidOperationException("Maximum file size must be positive");
        }

        if (FromDate.HasValue && ToDate.HasValue && FromDate > ToDate)
        {
            throw new InvalidOperationException("FromDate cannot be later than ToDate");
        }
    }
}