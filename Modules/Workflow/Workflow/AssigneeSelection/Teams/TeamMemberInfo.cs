namespace Workflow.AssigneeSelection.Teams;

public record TeamMemberInfo(
    string UserId,
    string DisplayName,
    string TeamId,
    List<string> ActivityGroups);
