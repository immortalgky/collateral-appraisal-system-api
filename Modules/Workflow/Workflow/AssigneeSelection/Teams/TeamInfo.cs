namespace Workflow.AssigneeSelection.Teams;

public record TeamInfo(
    string TeamId,
    string Name,
    TeamType Type,
    bool IsActive);
