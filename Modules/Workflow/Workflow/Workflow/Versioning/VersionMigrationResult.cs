namespace Workflow.Workflow.Versioning;

/// <summary>
/// Result of a workflow version migration operation
/// </summary>
public class VersionMigrationResult
{
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public int TotalInstancesProcessed { get; set; }
    public int SuccessfulMigrations { get; set; }
    public int FailedMigrations { get; set; }
    public TimeSpan Duration { get; set; }
    public List<MigrationError> Errors { get; set; } = new();
    public Dictionary<string, object> Statistics { get; set; } = new();
    public DateTime MigratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Error that occurred during workflow migration
/// </summary>
public class MigrationError
{
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public string ActivityId { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public bool IsCritical { get; set; }
}