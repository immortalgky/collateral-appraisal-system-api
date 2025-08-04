namespace Assignment.Tasks.Models;

public class RoundRobinQueue
{
    public string ActivityName { get; set; } = string.Empty;
    public string GroupsHash { get; set; } = string.Empty;
    public string GroupsList { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int AssignmentCount { get; set; } = 0;
    public DateTime LastAssignedAt { get; set; }
    public bool IsActive { get; set; } = true;
}