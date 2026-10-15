using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.AssigneeSelection.Services;
using Workflow.Services.Configuration;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Activities;

/// <summary>
/// Automatic activity that selects an internal followup staff member: the person who completed the
/// configured <c>sameAssigneeAsActivity</c> source activity (e.g. the PMA input), else a staff member
/// already selected by admin, else round-robin.
/// Sits between company-selection and ext-appraisal-assignment in the workflow.
/// </summary>
public class InternalFollowupSelectionActivity : WorkflowActivityBase
{
    private readonly IInternalStaffRoundRobinService _staffRoundRobinService;
    private readonly ITaskConfigurationService _configurationService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly ILogger<InternalFollowupSelectionActivity> _logger;

    public InternalFollowupSelectionActivity(
        IInternalStaffRoundRobinService staffRoundRobinService,
        ITaskConfigurationService configurationService,
        IDateTimeProvider dateTimeProvider,
        IIntegrationEventOutbox outbox,
        ILogger<InternalFollowupSelectionActivity> logger)
    {
        _staffRoundRobinService = staffRoundRobinService;
        _configurationService = configurationService;
        _dateTimeProvider = dateTimeProvider;
        _outbox = outbox;
        _logger = logger;
    }

    private const string StaffIdKey = "internalFollowupStaffId";
    private const string MethodKey = "internalFollowupMethod";
    private const string DecisionKey = "decision";
    private const string StaffSelectedDecision = "staff_selected";

    public override string ActivityType => ActivityTypes.InternalFollowupSelectionActivity;
    public override string Name => "Internal Followup Selection Activity";
    public override string Description => "Selects internal followup staff from a source activity's completer, admin selection or round-robin";

    protected override async Task<ActivityResult> ExecuteActivityAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        var existingStaffId = GetVariable<string>(context, StaffIdKey, "");
        var existingMethod = GetVariable<string>(context, MethodKey, "");

        var outputData = new Dictionary<string, object>
        {
            ["selectedAt"] = _dateTimeProvider.ApplicationNow
        };

        // The source activity's completer wins, even over an admin-selected staff member.
        var sourceCompleter = await ResolveSourceCompleterAsync(context, cancellationToken);
        if (sourceCompleter is not null)
        {
            // "Manual" on purpose: the FE admin page only accepts manual/roundrobin, and Manual means a
            // pre-determined person rather than a round-robin pick.
            outputData[StaffIdKey] = sourceCompleter;
            outputData[MethodKey] = "Manual";
            outputData[DecisionKey] = StaffSelectedDecision;

            _logger.LogInformation(
                "InternalFollowupSelectionActivity {ActivityId}: using {StaffId} who completed the source activity",
                context.ActivityId, sourceCompleter);

            PublishFollowupAssignedEvent(context, sourceCompleter, "Manual");
            return ActivityResult.Success(outputData);
        }

        // If admin already selected a followup staff, use it
        if (!string.IsNullOrEmpty(existingStaffId))
        {
            var method = string.IsNullOrEmpty(existingMethod) ? "Manual" : existingMethod;
            outputData[StaffIdKey] = existingStaffId;
            outputData[MethodKey] = method;
            outputData[DecisionKey] = StaffSelectedDecision;

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
            outputData[StaffIdKey] = result.UserId!;
            outputData[MethodKey] = "RoundRobin";
            outputData[DecisionKey] = StaffSelectedDecision;

            _logger.LogInformation(
                "InternalFollowupSelectionActivity {ActivityId}: round-robin selected staff {StaffId}",
                context.ActivityId, result.UserId);

            PublishFollowupAssignedEvent(context, result.UserId!, "RoundRobin");
            return ActivityResult.Success(outputData);
        }

        // No matching staff — still proceed but without followup staff
        outputData[DecisionKey] = "no_match";
        outputData["selectionError"] = result.ErrorMessage ?? "No eligible internal staff";

        _logger.LogWarning(
            "InternalFollowupSelectionActivity {ActivityId}: no match. Error: {Error}",
            context.ActivityId, result.ErrorMessage);

        return ActivityResult.Success(outputData);
    }

    // Source activity: DB override AdditionalConfiguration first (scoped like AssignmentContextBuilder), then the JSON
    // properties. Returns the newest completer of that activity, or null when no source is set or nobody completed it.
    private async Task<string?> ResolveSourceCompleterAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        var config = await _configurationService.GetConfigurationAsync(
            context.ActivityId,
            context.WorkflowInstance.WorkflowDefinitionId.ToString(),
            JsonPropertyReader.NullIfEmpty(JsonPropertyReader.GetString(context.Variables, "bankingSegment")),
            cancellationToken);

        var properties = JsonPropertyReader.Overlay(context.Properties, config?.AdditionalConfiguration);
        var source = JsonPropertyReader.GetString(properties, JsonPropertyReader.SameAssigneeAsActivityKey);

        return string.IsNullOrWhiteSpace(source)
            ? null
            : WorkflowActivityExecution.NewestCompletedBy(context.WorkflowInstance.ActivityExecutions, source);
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
