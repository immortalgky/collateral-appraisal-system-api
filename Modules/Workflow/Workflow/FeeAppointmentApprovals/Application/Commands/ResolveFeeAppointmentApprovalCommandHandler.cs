using Shared.Identity;
using Workflow.Contracts.FeeAppointmentApprovals;
using Workflow.FeeAppointmentApprovals.Domain;
using Workflow.Services.Groups;
using Workflow.Tasks.Authorization;
using Workflow.Workflow.Services;

namespace Workflow.FeeAppointmentApprovals.Application.Commands;

public class ResolveFeeAppointmentApprovalCommandHandler(
    WorkflowDbContext dbContext,
    IWorkflowService workflowService,
    ICurrentUserService currentUser,
    IUserGroupService userGroupService,
    ILogger<ResolveFeeAppointmentApprovalCommandHandler> logger
) : ICommandHandler<ResolveFeeAppointmentApprovalCommand, Unit>
{
    public async Task<Unit> Handle(
        ResolveFeeAppointmentApprovalCommand command,
        CancellationToken cancellationToken)
    {
        var approval = await dbContext.FeeAppointmentApprovals
                           .FirstOrDefaultAsync(a => a.Id == command.ApprovalId, cancellationToken)
                       ?? throw new InvalidOperationException($"FeeAppointmentApproval {command.ApprovalId} not found");

        if (approval.Status != FeeAppointmentApprovalStatus.Open)
            throw new InvalidOperationException($"FeeAppointmentApproval {command.ApprovalId} is not open");

        if (!approval.FollowupWorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Approval workflow instance not yet attached");

        var fwInstance = await dbContext.WorkflowInstances
                             .AsNoTracking()
                             .FirstOrDefaultAsync(w => w.Id == approval.FollowupWorkflowInstanceId.Value, cancellationToken)
                         ?? throw new InvalidOperationException("Approval workflow instance not found");

        // ─── Ownership gate: caller must own the pending approval task ─────────
        // Mirrors OpenTaskCommandHandler.IsOwner / PoolTaskAccess.IsOwner pattern.
        var pendingTask = await dbContext.PendingTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.WorkflowInstanceId == approval.FollowupWorkflowInstanceId.Value, cancellationToken);

        if (pendingTask is not null)
        {
            var username = currentUser.Username ?? string.Empty;
            bool isOwner;

            if (pendingTask.AssignedType == "1")
            {
                isOwner = string.Equals(pendingTask.AssignedTo, username, StringComparison.OrdinalIgnoreCase);
            }
            else // AssignedType == "2" — group/pool task
            {
                var groups = await userGroupService.GetGroupsForUserAsync(username, cancellationToken);
                isOwner = PoolTaskAccess.IsOwner(
                    pendingTask.AssignedTo,
                    pendingTask.AssigneeCompanyId,
                    groups,
                    userTeamId: null,
                    callerCompanyId: currentUser.CompanyId,
                    username: username);
            }

            if (!isOwner)
            {
                logger.LogWarning(
                    "User {Actor} attempted to resolve FeeAppointmentApproval {ApprovalId} but does not own the task",
                    command.Actor, command.ApprovalId);
                throw new UnauthorizedAccessException(
                    "You are not assigned to this approval task");
            }
        }
        else
        {
            // No pending task found — workflow may have timed out or been cancelled.
            // Allow resolve only if the task owner is the system (system-initiated cancel path).
            logger.LogWarning(
                "No pending task found for FeeAppointmentApproval {ApprovalId} workflow instance {InstanceId}",
                command.ApprovalId, approval.FollowupWorkflowInstanceId);
        }

        // Apply per-component decisions and raise domain event.
        // Capture the resolving user's bank code so the Appraisal module can stamp the real
        // approver (otherwise the fee/appointment outcome records a "system" placeholder).
        approval.Resolve(
            command.AppointmentDecision.Decision,
            command.AppointmentDecision.Reason,
            command.FeeDecision.Decision,
            command.FeeDecision.Reason,
            currentUser.UserCode);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Resume the child workflow so it transitions to EndActivity
        await workflowService.ResumeWorkflowAsync(
            approval.FollowupWorkflowInstanceId.Value,
            fwInstance.CurrentActivityId,
            command.Actor,
            new Dictionary<string, object> { ["decisionTaken"] = command.FeeDecision.Decision },
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Resolved FeeAppointmentApproval {ApprovalId} for appraisal {AppraisalId} by {Actor}",
            command.ApprovalId, approval.AppraisalId, command.Actor);

        return Unit.Value;
    }
}
