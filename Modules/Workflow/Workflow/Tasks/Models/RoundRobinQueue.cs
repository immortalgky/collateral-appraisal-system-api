namespace Workflow.Tasks.Models;

public class RoundRobinQueue
{
    public string ActivityName { get; set; } = string.Empty;
    public string GroupsHash { get; set; } = string.Empty;
    public string GroupsList { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int AssignmentCount { get; set; } = 0;

    /// <summary>
    /// Relative weight in the weighted round-robin. A company with weight 3 is selected three
    /// times as often as one with weight 1. Default 1 reproduces plain (unweighted) rotation.
    /// </summary>
    public int Weight { get; set; } = 1;

    public DateTime LastAssignedAt { get; set; }
    public bool IsActive { get; set; } = true;
}