namespace Workflow.Workflow.ImportExport;

/// <summary>
/// Result of a workflow import operation
/// </summary>
public class WorkflowImportResult
{
    /// <summary>
    /// Whether the import was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// IDs of imported workflow definitions
    /// </summary>
    public List<Guid> ImportedWorkflowIds { get; set; } = new();

    /// <summary>
    /// Number of workflows successfully imported
    /// </summary>
    public int SuccessfulImports { get; set; }

    /// <summary>
    /// Number of workflows that failed to import
    /// </summary>
    public int FailedImports { get; set; }

    /// <summary>
    /// Errors that occurred during import
    /// </summary>
    public List<WorkflowImportError> Errors { get; set; } = new();

    /// <summary>
    /// Warnings generated during import
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Import execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Size of imported data
    /// </summary>
    public long DataSizeBytes { get; set; }

    /// <summary>
    /// Import metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Import summary statistics
    /// </summary>
    public WorkflowImportStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Conflicts that were resolved during import
    /// </summary>
    public List<WorkflowConflictResolution> ResolvedConflicts { get; set; } = new();

    /// <summary>
    /// When the import was completed
    /// </summary>
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Create a successful import result
    /// </summary>
    public static WorkflowImportResult Success(List<Guid> importedIds, TimeSpan executionTime)
    {
        return new WorkflowImportResult
        {
            IsSuccess = true,
            ImportedWorkflowIds = importedIds,
            SuccessfulImports = importedIds.Count,
            ExecutionTime = executionTime
        };
    }

    /// <summary>
    /// Create a failed import result
    /// </summary>
    public static WorkflowImportResult Failure(string errorMessage, Exception? exception = null)
    {
        return new WorkflowImportResult
        {
            IsSuccess = false,
            Errors = new List<WorkflowImportError>
            {
                new WorkflowImportError
                {
                    Message = errorMessage,
                    Exception = exception,
                    IsCritical = true
                }
            }
        };
    }
}

/// <summary>
/// Error that occurred during import
/// </summary>
public class WorkflowImportError
{
    public string Message { get; set; } = string.Empty;
    public string? WorkflowName { get; set; }
    public int? LineNumber { get; set; }
    public string? PropertyPath { get; set; }
    public Exception? Exception { get; set; }
    public bool IsCritical { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
}

/// <summary>
/// Statistics about the import operation
/// </summary>
public class WorkflowImportStatistics
{
    public int TotalWorkflows { get; set; }
    public int ActivitiesImported { get; set; }
    public int VariablesImported { get; set; }
    public int ExpressionsImported { get; set; }
    public int ConflictsDetected { get; set; }
    public int ConflictsResolved { get; set; }
    public Dictionary<string, int> ActivityTypeCount { get; set; } = new();
}

/// <summary>
/// Information about a conflict that was resolved during import
/// </summary>
public class WorkflowConflictResolution
{
    public string ConflictType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public string? AffectedWorkflow { get; set; }
    public string? AffectedProperty { get; set; }
}