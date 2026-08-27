using Shared.Identity;
using Shared.Time;
using Workflow.AssigneeSelection.Teams;
using Workflow.Services.Groups;
using Workflow.Tasks.Authorization;
using Workflow.Data;
using Workflow.Tasks.ValueObjects;
using Workflow.Workflow.Services;

namespace Workflow.Tasks.Features.StartTask;

public class StartTaskCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUserService,
    IWorkflowNotificationService notificationService,
    ILogger<StartTaskCommandHandler> logger,
    IDateTimeProvider dateTimeProvider,
    IUserGroupService userGroupService,
    ITeamService teamService
) : ICommandHandler<StartTaskCommand, StartTaskResult>
{
    public async Task<StartTaskResult> Handle(StartTaskCommand command, CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new StartTaskResult(false, "User not authenticated");

        var task = await dbContext.PendingTasks.FindAsync([command.TaskId], cancellationToken);
        if (task is null)
            return new StartTaskResult(false, "Task not found");

        if (task.TaskStatus == TaskStatus.InProgress)
            return new StartTaskResult(false, $"Task is already being worked on by {task.WorkingBy}");

        // Ownership gate, matching SaveTaskDecisionDraft and OpenTask: pool tasks (AssignedType "2")
        // are owned by group/team/company membership, direct assignments by exact username. Without
        // this any authenticated user could start an arbitrary task id, taking WorkingBy and — since
        // the stamp is then >= AssigneeAssignedAt and so never self-heals — leaving the real
        // holder's open time permanently reading as a stranger's.
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
            return new StartTaskResult(false, "You are not the owner of this task");

        task.StartWorking(username, dateTimeProvider.ApplicationNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {Username} started working on task {TaskId}", username, command.TaskId);

        // Push real-time notification for pool tasks
        if (task.AssignedType == "2")
        {
            await notificationService.NotifyPoolTaskStarted(
                task.AssignedTo, command.TaskId, username);
        }

        return new StartTaskResult(true);
    }
}
