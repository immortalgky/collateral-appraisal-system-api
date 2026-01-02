namespace Workflow.Workflow.ImportExport;

/// <summary>
/// Supported formats for workflow import/export
/// </summary>
public enum WorkflowExportFormat
{
    /// <summary>
    /// JSON format with full metadata
    /// </summary>
    Json,

    /// <summary>
    /// YAML format for human-readable exports
    /// </summary>
    Yaml,

    /// <summary>
    /// XML format for enterprise integrations
    /// </summary>
    Xml,

    /// <summary>
    /// Binary format for compact storage
    /// </summary>
    Binary,

    /// <summary>
    /// Excel format for business users
    /// </summary>
    Excel,

    /// <summary>
    /// BPMN 2.0 standard format
    /// </summary>
    Bpmn,

    /// <summary>
    /// Custom compressed package format
    /// </summary>
    Package
}

/// <summary>
/// Format information and capabilities
/// </summary>
public class WorkflowFormatInfo
{
    public WorkflowExportFormat Format { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public bool SupportsMetadata { get; set; }
    public bool SupportsVersioning { get; set; }
    public bool SupportsCompression { get; set; }
    public bool IsHumanReadable { get; set; }
    public CompressionLevel DefaultCompression { get; set; }
}

/// <summary>
/// Compression levels for exported data
/// </summary>
public enum CompressionLevel
{
    None,
    Fast,
    Optimal,
    Maximum
}