namespace Workflow.Workflow.Versioning;

/// <summary>
/// Represents a breaking change between workflow schema versions
/// </summary>
public class BreakingChange
{
    public BreakingChangeType Type { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BreakingChangeSeverity Severity { get; set; }
    public string[] AffectedInstances { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Types of breaking changes in workflow schemas
/// </summary>
public enum BreakingChangeType
{
    ActivityRemoved,
    PropertyTypeChanged,
    RequiredPropertyAdded,
    PropertyRemoved,
    ConnectionModified,
    ValidationRuleChanged,
    DataTypeIncompatible
}

/// <summary>
/// Severity levels for breaking changes
/// </summary>
public enum BreakingChangeSeverity
{
    Low,
    Medium,
    High,
    Critical
}