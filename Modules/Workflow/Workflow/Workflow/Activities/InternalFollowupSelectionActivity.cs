using Workflow.AssigneeSelection.Services;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Activities;

/// <summary>
/// Automatic activity that selects an internal followup staff member via round-robin
/// or uses a staff member already selected by admin.
/// Sits between company-selection and ext-appraisal-assignment in the workflow.
/// </summary>
public class InternalFollowupSelectionActivity : WorkflowActivityBase
{
    private readonly IInternalStaffRoundRobinService _staffRoundRobinService;
    private readonly ILogger<InternalFollowupSelectionActivity> _logger;

    public InternalFollowupSelectionActivity(
        IInternalStaffRoundRobinService staffRoundRobinService,
        ILogger<InternalFollowupSelectionActivity> logger)
    {
        _staffRoundRobinService = staffRoundRobinService;
        _logger = logger;
    }

    public override string ActivityType => ActivityTypes.InternalFollowupSelectionActivity;
    public override string Name => "Internal Followup Selection Activity";
    public override string Description => "Selects internal followup staff via round-robin or uses admin-selected staff";

    protected override async Task<ActivityResult> ExecuteActivityAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        var existingStaffId = GetVariable<string>(context, "internalFollowupStaffId", "");
        var existingMethod = GetVariable<string>(context, "internalFollowupMethod", "");

        var outputData = new Dictionary<string, object>
        {
            ["selectedAt"] = DateTime.UtcNow
        };

        // If admin already selected a followup staff, use it
        if (!string.IsNullOrEmpty(existingStaffId))
        {
            outputData["internalFollowupStaffId"] = existingStaffId;
            outputData["internalFollowupMethod"] = string.IsNullOrEmpty(existingMethod) ? "Manual" : existingMethod;
            outputData["decision"] = "staff_selected";

            _logger.LogInformation(
                "InternalFollowupSelectionActivity {ActivityId}: using admin-selected staff {StaffId}",
                context.ActivityId, existingStaffId);

            return ActivityResult.Success(outputData);
        }

        // Round-robin select from IntAppraisalStaff group
        var result = await _staffRoundRobinService.SelectStaffAsync(cancellationToken);

        if (result.IsSuccess)
        {
            outputData["internalFollowupStaffId"] = result.UserId!;
            outputData["internalFollowupMethod"] = "RoundRobin";
            outputData["decision"] = "staff_selected";

            _logger.LogInformation(
                "InternalFollowupSelectionActivity {ActivityId}: round-robin selected staff {StaffId}",
                context.ActivityId, result.UserId);

            return ActivityResult.Success(outputData);
        }

        // No matching staff — still proceed but without followup staff
        outputData["decision"] = "no_match";
        outputData["selectionError"] = result.ErrorMessage ?? "No eligible internal staff";

        _logger.LogWarning(
            "InternalFollowupSelectionActivity {ActivityId}: no match. Error: {Error}",
            context.ActivityId, result.ErrorMessage);

        return ActivityResult.Success(outputData);
    }

    protected override WorkflowActivityExecution CreateActivityExecution(ActivityContext context)
    {
        return WorkflowActivityExecution.Create(
            context.WorkflowInstance.Id,
            context.ActivityId,
            Name,
            ActivityType,
            "SYSTEM",
            context.Variables);
    }
}
