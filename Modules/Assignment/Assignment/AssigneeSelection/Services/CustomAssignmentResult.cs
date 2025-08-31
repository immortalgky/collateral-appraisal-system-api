using Assignment.AssigneeSelection.Core;

namespace Assignment.AssigneeSelection.Services;

/// <summary>
/// Result of custom assignment service execution
/// </summary>
public class CustomAssignmentResult
{
    /// <summary>
    /// Indicates whether the service determined a custom assignment should be used
    /// If false, the system will fall back to standard assignment logic
    /// </summary>
    public bool UseCustomAssignment { get; set; }

    /// <summary>
    /// Specific user to assign the task to (takes precedence over SpecificGroup)
    /// </summary>
    public string? SpecificAssignee { get; set; }

    /// <summary>
    /// Specific group to assign the task to (used if SpecificAssignee is not provided)
    /// </summary>
    public string? SpecificGroup { get; set; }

    /// <summary>
    /// Custom assignment strategies to use instead of workflow definition strategies
    /// </summary>
    public List<string>? CustomStrategies { get; set; }

    /// <summary>
    /// Custom properties to override workflow definition properties
    /// Can include taskWeight, skillsRequired, priorityLevel, etc.
    /// </summary>
    public Dictionary<string, object>? CustomProperties { get; set; }

    /// <summary>
    /// Human-readable reason for the custom assignment decision
    /// Used for audit trail and debugging
    /// </summary>
    public string Reason { get; set; } = default!;

    /// <summary>
    /// Additional metadata about the assignment decision
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Creates a successful custom assignment result with specific assignee
    /// </summary>
    /// <param name="assignee">The specific user to assign to</param>
    /// <param name="reason">Reason for this assignment</param>
    /// <returns>Custom assignment result</returns>
    public static CustomAssignmentResult ForAssignee(string assignee, string reason)
    {
        return new CustomAssignmentResult
        {
            UseCustomAssignment = true,
            SpecificAssignee = assignee,
            Reason = reason
        };
    }

    /// <summary>
    /// Creates a successful custom assignment result with specific group
    /// </summary>
    /// <param name="group">The specific group to assign to</param>
    /// <param name="reason">Reason for this assignment</param>
    /// <returns>Custom assignment result</returns>
    public static CustomAssignmentResult ForGroup(string group, string reason)
    {
        return new CustomAssignmentResult
        {
            UseCustomAssignment = true,
            SpecificGroup = group,
            Reason = reason
        };
    }

    /// <summary>
    /// Creates a successful custom assignment result with custom strategies
    /// </summary>
    /// <param name="strategies">Custom assignment strategies to use</param>
    /// <param name="reason">Reason for using these strategies</param>
    /// <returns>Custom assignment result</returns>
    public static CustomAssignmentResult ForStrategies(List<string> strategies, string reason)
    {
        return new CustomAssignmentResult
        {
            UseCustomAssignment = true,
            CustomStrategies = strategies,
            Reason = reason
        };
    }

    /// <summary>
    /// Creates a result indicating no custom assignment should be used
    /// </summary>
    /// <param name="reason">Optional reason for not using custom assignment</param>
    /// <returns>Custom assignment result indicating standard logic should be used</returns>
    public static CustomAssignmentResult NoCustomAssignment(string? reason = null)
    {
        return new CustomAssignmentResult
        {
            UseCustomAssignment = false,
            Reason = reason ?? "No custom assignment logic applicable"
        };
    }
}