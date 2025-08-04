namespace Assignment.AssigneeSelection.Core;

public class AssignmentContext
{
    public long RequestId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public List<string> UserGroups { get; set; } = new();
    public string? RequiredRole { get; set; }
    public List<string>? RequiredSkills { get; set; }
    public int? Priority { get; set; }
    public DateTime DueDate { get; set; }
    public Dictionary<string, object>? AdditionalCriteria { get; set; }
}