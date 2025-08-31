namespace Assignment.AssigneeSelection.Core;

public class AssignmentContext
{
    public string ActivityName { get; set; } = default!;
    public List<string> AssignmentStrategies { get; set; } = new();
    public List<string> UserGroups { get; set; } = new();
    public string UserCode { get; set; } = default!;
    public DateTime DueDate { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
}