using Shared.Identity;
using Workflow.AssigneeSelection.Teams;
using Workflow.Data;
using Workflow.Services.Groups;
using Workflow.Tasks.Authorization;

namespace Workflow.Tasks.Features.SaveTaskDecisionDraft;

public class SaveTaskDecisionDraftCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUserService,
    IUserGroupService userGroupService,
    ITeamService teamService,
    ILogger<SaveTaskDecisionDraftCommandHandler> logger
) : ICommandHandler<SaveTaskDecisionDraftCommand, SaveTaskDecisionDraftResult>
{
    public async Task<SaveTaskDecisionDraftResult> Handle(SaveTaskDecisionDraftCommand command, CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new SaveTaskDecisionDraftResult(false, ErrorMessage: "User not authenticated");

        var task = await dbContext.PendingTasks.FindAsync([command.TaskId], cancellationToken);
        if (task is null)
            return new SaveTaskDecisionDraftResult(false, ErrorMessage: "Task not found");

        // Same ownership rule as GetTaskById: pool tasks (AssignedType == "2") are owned by
        // group/team/company membership, direct-assignment tasks by exact username match.
        bool isOwner;
        if (task.AssignedType == "2")
        {
            var groups = await userGroupService.GetGroupsForUserAsync(username, cancellationToken);
            var team = await teamService.GetTeamForUserAsync(username, cancellationToken);
            isOwner = PoolTaskAccess.IsOwner(
                task.AssignedTo,
                task.AssigneeCompanyId,
                groups,
                team?.TeamId,
                currentUserService.CompanyId,
                username);
        }
        else
        {
            isOwner = string.Equals(task.AssignedTo, username, StringComparison.OrdinalIgnoreCase);
        }

        if (!isOwner)
            return new SaveTaskDecisionDraftResult(false, IsForbidden: true, ErrorMessage: "You are not the owner of this task");

        task.SaveDecisionDraft(command.DecisionTaken, command.Comment, command.ReasonCode, command.Assignee);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {Username} saved decision draft for task {TaskId}", username, command.TaskId);

        return new SaveTaskDecisionDraftResult(true);
    }
}
