namespace Workflow.Workflow.Activities.Core;

/// <summary>
/// Represents runtime assignment overrides that can be provided via API calls
/// These overrides take precedence over workflow definition and external configuration
/// </summary>
public class RuntimeOverride
{
    /// <summary>
    /// Specific user to assign the task to (overrides all assignment strategies)
    /// </summary>
    public string? RuntimeAssignee { get; set; }

    /// <summary>
    /// Specific group to assign the task to (used if RuntimeAssignee is not provided)
    /// </summary>
    public string? RuntimeAssigneeGroup { get; set; }

    /// <summary>
    /// Custom assignment strategies to use instead of workflow definition strategies
    /// </summary>
    public List<string>? RuntimeAssignmentStrategies { get; set; }

    /// <summary>
    /// Reason for the runtime override (used for audit trail)
    /// </summary>
    public string? OverrideReason { get; set; }

    /// <summary>
    /// Additional properties to override workflow definition properties
    /// </summary>
    public Dictionary<string, object>? OverrideProperties { get; set; }

    /// <summary>
    /// User who initiated the override (for audit purposes)
    /// </summary>
    public string? OverrideBy { get; set; }

    /// <summary>
    /// Timestamp when the override was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Checks if this override has any assignment specifications
    /// </summary>
    public bool HasAssignmentOverride => 
        !string.IsNullOrEmpty(RuntimeAssignee) || 
        !string.IsNullOrEmpty(RuntimeAssigneeGroup) || 
        (RuntimeAssignmentStrategies?.Any() == true);

    /// <summary>
    /// Creates a runtime override for specific user assignment
    /// </summary>
    /// <param name="assignee">User to assign to</param>
    /// <param name="reason">Reason for override</param>
    /// <param name="overrideBy">User who initiated the override</param>
    /// <returns>Runtime override instance</returns>
    public static RuntimeOverride ForAssignee(string assignee, string reason, string? overrideBy = null)
    {
        return new RuntimeOverride
        {
            RuntimeAssignee = assignee,
            OverrideReason = reason,
            OverrideBy = overrideBy
        };
    }

    /// <summary>
    /// Creates a runtime override for group assignment
    /// </summary>
    /// <param name="group">Group to assign to</param>
    /// <param name="reason">Reason for override</param>
    /// <param name="overrideBy">User who initiated the override</param>
    /// <returns>Runtime override instance</returns>
    public static RuntimeOverride ForGroup(string group, string reason, string? overrideBy = null)
    {
        return new RuntimeOverride
        {
            RuntimeAssigneeGroup = group,
            OverrideReason = reason,
            OverrideBy = overrideBy
        };
    }

    /// <summary>
    /// Creates a runtime override for custom assignment strategies
    /// </summary>
    /// <param name="strategies">Assignment strategies to use</param>
    /// <param name="reason">Reason for override</param>
    /// <param name="overrideBy">User who initiated the override</param>
    /// <returns>Runtime override instance</returns>
    public static RuntimeOverride ForStrategies(List<string> strategies, string reason, string? overrideBy = null)
    {
        return new RuntimeOverride
        {
            RuntimeAssignmentStrategies = strategies,
            OverrideReason = reason,
            OverrideBy = overrideBy
        };
    }
}