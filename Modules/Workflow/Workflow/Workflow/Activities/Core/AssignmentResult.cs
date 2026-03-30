namespace Workflow.Workflow.Activities.Core;

/// <summary>
/// Result of assignment determination process
/// </summary>
public class AssignmentResult
{
    public bool IsSuccess { get; set; }
    public string? AssigneeId { get; set; }
    public string? Strategy { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public string? ErrorMessage { get; set; }
}
