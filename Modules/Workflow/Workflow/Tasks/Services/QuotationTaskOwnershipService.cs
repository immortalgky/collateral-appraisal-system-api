using Appraisal.Contracts.Services;
using Shared.Identity;
using Workflow.Data.Repository;
using Workflow.Tasks.Authorization;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using WorkflowGroupService = global::Workflow.Services.Groups.IUserGroupService;
using WorkflowTeamService = global::Workflow.AssigneeSelection.Teams.ITeamService;

namespace Workflow.Tasks.Services;

/// <summary>
/// Implements <see cref="IQuotationTaskOwnershipService"/> for the two-person rule on quotation
/// fan-out tasks, and for RM direct-assignment tasks (e.g. rm-pick-winner).
///
/// Flow for external (company) callers:
///   1. Internal admins always pass.
///   2. Load the active fan-out PendingTask for (quotationRequestId, ext-collect-submissions, companyId).
///   3. Optionally gate on the current FanOut stage name.
///   4. Verify the caller owns the task via PoolTaskAccess.IsOwner (with username).
///
/// Flow for internal (RM) callers:
///   Active: fall through fan-out lookup (returns null for non-fan-out tasks), then check any
///   active PendingTask on the quotation that the RM directly owns.
///   Historical: check fan-out completed tasks first; if none match, also check non-fan-out
///   completed tasks via GetCompletedTasksByCorrelationIdForUserAsync.
/// </summary>
public class QuotationTaskOwnershipService(
    IAssignmentRepository assignmentRepository,
    IWorkflowInstanceRepository instanceRepository,
    ICurrentUserService currentUser,
    WorkflowGroupService userGroupService,
    WorkflowTeamService teamService)
    : IQuotationTaskOwnershipService
{
    private const string FanOutActivityId = "ext-collect-submissions";

    public async Task<bool> IsCallerActiveTaskOwnerAsync(
        Guid quotationRequestId,
        Guid companyId,
        string? expectedStageName,
        CancellationToken cancellationToken = default)
    {
        // 1. Internal admins bypass the ownership gate
        if (currentUser.IsInRole("Admin") || currentUser.IsInRole("IntAdmin"))
            return true;

        var username = currentUser.Username;
        if (string.IsNullOrEmpty(username))
            return false;

        var groups = await userGroupService.GetGroupsForUserAsync(username, cancellationToken);
        var team   = await teamService.GetTeamForUserAsync(username, cancellationToken);

        // 2. Find the active fan-out task for this company on this quotation
        var task = await assignmentRepository.GetFanOutTaskByCorrelationIdAndCompanyAsync(
            quotationRequestId,
            FanOutActivityId,
            companyId,
            cancellationToken);

        if (task is not null)
        {
            // 3. Resolve the real current stage from WorkflowActivityExecution.FanOutItems.
            //    task.TaskName == FanOutActivityId, NOT the stage name.
            //    The stage ("maker" / "checker") lives on FanOutItemState.CurrentStage.
            if (expectedStageName is not null)
            {
                var instance = await instanceRepository.GetWithExecutionsAsync(
                    task.WorkflowInstanceId, cancellationToken);

                if (instance is not null)
                {
                    var execution = instance.ActivityExecutions
                        .FirstOrDefault(e => e.ActivityId == task.ActivityId
                                            && e.Status == ActivityExecutionStatus.InProgress);

                    var currentStage = execution?.FanOutItems
                        .FirstOrDefault(i => i.FanOutKey == companyId)?.CurrentStage;

                    // When currentStage is null (legacy no-stage fan-out or execution not found)
                    // skip the stage gate and let PoolTaskAccess.IsOwner be the sole arbiter.
                    if (currentStage is not null
                        && !string.Equals(currentStage, expectedStageName, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            // 4. Verify the caller holds the fan-out task.
            return PoolTaskAccess.IsOwner(
                task.AssignedTo,
                task.AssigneeCompanyId,
                groups,
                team?.TeamId,
                currentUser.CompanyId,
                username);
        }

        // 5. RM / non-fan-out path: check any active PendingTask on this quotation that the
        //    caller directly owns (e.g. rm-pick-winner, AssignedType='1').
        //    Only reached when no fan-out task exists — external company callers will have
        //    already found their task above or correctly get false here.
        var allActiveTasks = await assignmentRepository.GetPendingTasksByCorrelationIdAsync(
            quotationRequestId, cancellationToken);

        return allActiveTasks.Any(t => PoolTaskAccess.IsOwner(
            t.AssignedTo,
            t.AssigneeCompanyId,
            groups,
            team?.TeamId,
            currentUser.CompanyId,
            username));
    }

    public async Task<bool> IsCallerActivityTaskOwnerStrictAsync(
        Guid quotationRequestId,
        string activityId,
        CancellationToken cancellationToken = default)
    {
        // Strict: NO admin role bypass. Only the actual assignee passes.
        var username = currentUser.Username;
        if (string.IsNullOrEmpty(username))
            return false;

        var groups = await userGroupService.GetGroupsForUserAsync(username, cancellationToken);
        var team = await teamService.GetTeamForUserAsync(username, cancellationToken);

        var allActiveTasks = await assignmentRepository.GetPendingTasksByCorrelationIdAsync(
            quotationRequestId, cancellationToken);

        return allActiveTasks
            .Where(t => string.Equals(t.ActivityId, activityId, StringComparison.OrdinalIgnoreCase))
            .Any(t => PoolTaskAccess.IsOwner(
                t.AssignedTo,
                t.AssigneeCompanyId,
                groups,
                team?.TeamId,
                currentUser.CompanyId,
                username));
    }

    public async Task<bool> IsCallerHistoricalTaskOwnerAsync(
        Guid quotationRequestId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole("Admin") || currentUser.IsInRole("IntAdmin"))
            return true;

        var username = currentUser.Username;
        if (string.IsNullOrEmpty(username))
            return false;

        var groups = await userGroupService.GetGroupsForUserAsync(username, cancellationToken);
        var team   = await teamService.GetTeamForUserAsync(username, cancellationToken);

        // Check fan-out completed tasks (ext company callers)
        var fanOutTasks = await assignmentRepository.GetCompletedFanOutTasksByCorrelationIdAndCompanyAsync(
            quotationRequestId,
            companyId,
            cancellationToken);

        if (fanOutTasks.Any(t => PoolTaskAccess.IsOwner(
                t.AssignedTo,
                t.AssigneeCompanyId,
                groups,
                team?.TeamId,
                currentUser.CompanyId,
                username)))
            return true;

        // RM / non-fan-out path: check completed tasks for this quotation that the caller
        // directly owns regardless of company.
        var userTasks = await assignmentRepository.GetCompletedTasksByCorrelationIdForUserAsync(
            quotationRequestId,
            username,
            groups,
            team?.TeamId,
            currentUser.CompanyId,
            cancellationToken);

        return userTasks.Any(t => PoolTaskAccess.IsOwner(
            t.AssignedTo,
            t.AssigneeCompanyId,
            groups,
            team?.TeamId,
            currentUser.CompanyId,
            username));
    }
}
