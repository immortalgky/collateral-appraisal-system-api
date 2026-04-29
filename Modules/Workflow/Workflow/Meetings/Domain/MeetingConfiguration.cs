namespace Workflow.Meetings.Domain;

public class MeetingConfiguration
{
    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private MeetingConfiguration() { }
}
