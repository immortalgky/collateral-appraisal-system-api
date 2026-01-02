namespace Workflow.Workflow.Versioning;

/// <summary>
/// Estimation result for workflow version migration
/// </summary>
public class MigrationEstimate
{
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public int AffectedInstanceCount { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public MigrationRisk RiskLevel { get; set; }
    public List<BreakingChange> CriticalChanges { get; set; } = new();
    public List<string> RequiredActions { get; set; } = new();
    public bool RequiresDowntime { get; set; }
    public string[] RecommendedStrategy { get; set; } = Array.Empty<string>();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Risk levels for workflow migrations
/// </summary>
public enum MigrationRisk
{
    Low,
    Medium,
    High,
    Critical
}