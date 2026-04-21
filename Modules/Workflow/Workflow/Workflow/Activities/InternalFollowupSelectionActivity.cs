using Shared.Data.Outbox;
using Shared.Messaging.Events;
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
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly ILogger<InternalFollowupSelectionActivity> _logger;

    public InternalFollowupSelectionActivity(
        IInternalStaffRoundRobinService staffRoundRobinService,
        IDateTimeProvider dateTimeProvider,
        IIntegrationEventOutbox outbox,
        ILogger<InternalFollowupSelectionActivity> logger)
    {
        _staffRoundRobinService = staffRoundRobinService;
        _dateTimeProvider = dateTimeProvider;
        _outbox = outbox;
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
            ["selectedAt"] = _dateTimeProvider.ApplicationNow
        };

        // If admin already selected a followup staff, use it
        if (!string.IsNullOrEmpty(existingStaffId))
        {
            var method = string.IsNullOrEmpty(existingMethod) ? "Manual" : existingMethod;
            outputData["internalFollowupStaffId"] = existingStaffId;
            outputData["internalFollowupMethod"] = method;
            outputData["decision"] = "staff_selected";

            _logger.LogInformation(
                "InternalFollowupSelectionActivity {ActivityId}: using admin-selected staff {StaffId}",
                context.ActivityId, existingStaffId);

            PublishFollowupAssignedEvent(context, existingStaffId, method);
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

            PublishFollowupAssignedEvent(context, result.UserId!, "RoundRobin");
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

    private void PublishFollowupAssignedEvent(
        ActivityContext context,
        string internalAppraiserId,
        string internalFollowupMethod)
    {
        var appraisalId = WorkflowVariables.TryGetAppraisalId(context.Variables);
        if (appraisalId is null)
        {
            _logger.LogWarning(
                "InternalFollowupSelectionActivity {ActivityId}: appraisalId not in variables; skipping InternalFollowupAssignedIntegrationEvent publish",
                context.ActivityId);
            return;
        }

        _outbox.Publish(new InternalFollowupAssignedIntegrationEvent
        {
            AppraisalId = appraisalId.Value,
            InternalAppraiserId = internalAppraiserId,
            InternalFollowupAssignmentMethod = internalFollowupMethod,
            CompletedBy = context.WorkflowInstance.LastCompletedBy
        }, correlationId: appraisalId.Value.ToString());

        _logger.LogInformation(
            "InternalFollowupSelectionActivity {ActivityId}: published InternalFollowupAssignedIntegrationEvent for AppraisalId={AppraisalId}, StaffId={StaffId}",
            context.ActivityId, appraisalId.Value, internalAppraiserId);
    }
}
