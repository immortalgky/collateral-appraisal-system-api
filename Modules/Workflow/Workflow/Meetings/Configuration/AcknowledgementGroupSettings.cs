namespace Workflow.Meetings.Configuration;

/// <summary>
/// Maps committee code → acknowledgement group name.
/// Configured under <c>Workflow:AcknowledgementGroupByCommitteeCode</c> in appsettings.
/// </summary>
public class AcknowledgementGroupSettings
{
    public const string SectionName = "Workflow";

    /// <summary>
    /// Key: committee code (e.g. "SUB", "GRP1").
    /// Value: acknowledgement group name (e.g. "Group1", "UrgentGroup2").
    /// </summary>
    public Dictionary<string, string> AcknowledgementGroupByCommitteeCode { get; set; } = new();
}
