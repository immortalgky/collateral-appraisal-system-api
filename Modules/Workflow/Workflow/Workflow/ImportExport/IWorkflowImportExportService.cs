namespace Workflow.Workflow.ImportExport;

/// <summary>
/// Service for importing and exporting workflow definitions in various formats
/// </summary>
public interface IWorkflowImportExportService
{
    /// <summary>
    /// Export workflow definition to specified format
    /// </summary>
    Task<WorkflowExportResult> ExportWorkflowAsync(Guid workflowDefinitionId, WorkflowExportFormat format, WorkflowExportOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Import workflow definition from specified format
    /// </summary>
    Task<WorkflowImportResult> ImportWorkflowAsync(string workflowData, WorkflowExportFormat format, WorkflowImportOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Export multiple workflow definitions as a package
    /// </summary>
    Task<WorkflowExportResult> ExportWorkflowPackageAsync(IEnumerable<Guid> workflowDefinitionIds, WorkflowExportFormat format, WorkflowExportOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Import multiple workflow definitions from a package
    /// </summary>
    Task<WorkflowImportResult> ImportWorkflowPackageAsync(string packageData, WorkflowExportFormat format, WorkflowImportOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate workflow data without importing
    /// </summary>
    Task<WorkflowValidationResult> ValidateWorkflowDataAsync(string workflowData, WorkflowExportFormat format, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get supported export formats
    /// </summary>
    Task<IEnumerable<WorkflowExportFormat>> GetSupportedFormatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert between different workflow formats
    /// </summary>
    Task<WorkflowConversionResult> ConvertWorkflowFormatAsync(string workflowData, WorkflowExportFormat fromFormat, WorkflowExportFormat toFormat, CancellationToken cancellationToken = default);

    /// <summary>
    /// Export workflow execution history
    /// </summary>
    Task<WorkflowExportResult> ExportExecutionHistoryAsync(Guid workflowInstanceId, WorkflowExportFormat format, CancellationToken cancellationToken = default);
}