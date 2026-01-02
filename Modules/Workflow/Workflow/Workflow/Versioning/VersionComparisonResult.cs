namespace Workflow.Workflow.Versioning;

/// <summary>
/// Result of comparing two workflow schema versions
/// </summary>
public class VersionComparisonResult
{
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public bool IsCompatible { get; set; }
    public List<BreakingChange> BreakingChanges { get; set; } = new();
    public List<string> AddedActivities { get; set; } = new();
    public List<string> RemovedActivities { get; set; } = new();
    public List<string> ModifiedActivities { get; set; } = new();
    public DateTime ComparedAt { get; set; } = DateTime.UtcNow;
}