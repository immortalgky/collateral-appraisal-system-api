using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.AssigneeSelection.Services;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Activities;

/// <summary>
/// Automatic activity that selects an external company via round-robin or manual assignment.
/// Sits between routing/admin and ext-appraisal-staff in the workflow.
/// </summary>
public class CompanySelectionActivity : WorkflowActivityBase
{
    private readonly ICompanyRoundRobinService _companyRoundRobinService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly ILogger<CompanySelectionActivity> _logger;

    public CompanySelectionActivity(
        ICompanyRoundRobinService companyRoundRobinService,
        IDateTimeProvider dateTimeProvider,
        IIntegrationEventOutbox outbox,
        ILogger<CompanySelectionActivity> logger)
    {
        _companyRoundRobinService = companyRoundRobinService;
        _dateTimeProvider = dateTimeProvider;
        _outbox = outbox;
        _logger = logger;
    }

    public override string ActivityType => ActivityTypes.CompanySelectionActivity;
    public override string Name => "Company Selection Activity";
    public override string Description => "Selects external company via round-robin or manual assignment";

    protected override async Task<ActivityResult> ExecuteActivityAsync(
        ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        var selectionMethod = GetVariable<string>(context, "assignmentMethod", "round_robin");
        var loanType = GetVariable<string>(context, "loanType", "");
        var excludedCompanyId = GetVariable<string>(context, "excludedCompanyId", "");

        var outputData = new Dictionary<string, object>
        {
            ["selectionMethod"] = selectionMethod,
            ["selectedAt"] = _dateTimeProvider.ApplicationNow,
            ["assignmentType"] = "External"
        };

        // Construction Inspection: company is forced from the prior appraisal engagement.
        // Short-circuit selection entirely — no round-robin, no exclusion check.
        var forceCompanyId = GetVariable<string>(context, "forceCompanyId", "");
        var forceCompanyName = GetVariable<string>(context, "forceCompanyName", "");

        if (!string.IsNullOrEmpty(forceCompanyId) && Guid.TryParse(forceCompanyId, out _))
        {
            outputData["assignedCompanyId"] = forceCompanyId;
            outputData["assignedCompanyName"] = forceCompanyName;
            outputData["assignmentMethod"] = "Forced";
            outputData["decision"] = "company_selected";

            _logger.LogInformation(
                "CompanySelectionActivity {ActivityId}: forced company {CompanyName} ({CompanyId}) for Construction Inspection",
                context.ActivityId, forceCompanyName, forceCompanyId);

            PublishCompanyAssignedEvent(context, forceCompanyId, forceCompanyName, "Forced");
            return ActivityResult.Success(outputData);
        }

        var isManual = string.Equals(selectionMethod, "manual", StringComparison.OrdinalIgnoreCase);
        var isQuotation = string.Equals(selectionMethod, "Quotation", StringComparison.OrdinalIgnoreCase);
        if (isManual || isQuotation)
        {
            var companyId = GetVariable<string>(context, "assignedCompanyId", "");
            if (string.IsNullOrEmpty(companyId))
                companyId = GetVariable<string>(context, "selectedCompanyId", "");
            var companyName = GetVariable<string>(context, "assignedCompanyName", "");
            if (string.IsNullOrEmpty(companyName))
                companyName = GetVariable<string>(context, "selectedCompanyName", "");

            if (string.IsNullOrEmpty(companyId))
            {
                _logger.LogWarning("CompanySelectionActivity {ActivityId}: {Method} selection but no company selected",
                    context.ActivityId, selectionMethod);
                return ActivityResult.Failed($"No company selected for {selectionMethod} assignment");
            }

            if (Guid.TryParse(excludedCompanyId, out var excl) &&
                Guid.TryParse(companyId, out var sel) &&
                excl == sel)
            {
                _logger.LogWarning(
                    "CompanySelectionActivity {ActivityId}: {Method} selected excluded company {CompanyId}",
                    context.ActivityId, selectionMethod, companyId);
                return ActivityResult.Failed("Selected company is excluded from this appraisal assignment.");
            }

            var normalizedMethod = isQuotation ? "Quotation" : "Manual";

            outputData["assignedCompanyId"] = companyId;
            outputData["assignedCompanyName"] = companyName;
            outputData["assignmentMethod"] = normalizedMethod;
            outputData["decision"] = "company_selected";

            _logger.LogInformation(
                "CompanySelectionActivity {ActivityId}: {Method} selected company {CompanyName} ({CompanyId})",
                context.ActivityId, normalizedMethod, companyName, companyId);

            // Publish CompanyAssignedIntegrationEvent for all external paths (Manual, Quotation, etc.).
            // CompanyAssignedIntegrationEventHandler is the sole mutation path; it resolves the fee
            // source based on AssignmentMethod (Quotation → quotation repo lookup; others → tier-based).
            PublishCompanyAssignedEvent(context, companyId, companyName, normalizedMethod);

            return ActivityResult.Success(outputData);
        }

        // Replay guard: reuse previously selected company if the selection condition (loanType) has not changed
        var existingCompanyId = GetVariable<string>(context, "assignedCompanyId", "");
        var existingCompanyName = GetVariable<string>(context, "assignedCompanyName", "");
        var existingLoanType = GetVariable<string>(context, "assignedCompanyLoanType", "");

        if (!string.IsNullOrEmpty(existingCompanyId) && existingLoanType == loanType)
        {
            outputData["assignedCompanyId"] = existingCompanyId;
            outputData["assignedCompanyName"] = existingCompanyName;
            outputData["assignmentMethod"] = selectionMethod;
            outputData["assignedCompanyLoanType"] = loanType;
            outputData["decision"] = "company_selected";

            _logger.LogInformation(
                "CompanySelectionActivity {ActivityId}: replaying — reusing previously selected company {CompanyName} ({CompanyId})",
                context.ActivityId, existingCompanyName, existingCompanyId);

            return ActivityResult.Success(outputData);
        }

        // Round-robin selection with optional loanType filter and company exclusion
        Guid? excludedId = null;
        if (!string.IsNullOrEmpty(excludedCompanyId) && Guid.TryParse(excludedCompanyId, out var parsedExcluded))
            excludedId = parsedExcluded;

        var result = await _companyRoundRobinService.SelectCompanyAsync(
            excludedId,
            string.IsNullOrEmpty(loanType) ? null : loanType,
            cancellationToken);

        if (result.IsSuccess)
        {
            outputData["assignedCompanyId"] = result.CompanyId!.Value.ToString();
            outputData["assignedCompanyName"] = result.CompanyName!;
            outputData["assignmentMethod"] = "RoundRobin";
            outputData["assignedCompanyLoanType"] = loanType;
            outputData["decision"] = "company_selected";

            _logger.LogInformation(
                "CompanySelectionActivity {ActivityId}: round-robin selected company {CompanyName} ({CompanyId})",
                context.ActivityId, result.CompanyName, result.CompanyId);

            PublishCompanyAssignedEvent(context, result.CompanyId!.Value.ToString(), result.CompanyName!, "RoundRobin");
            return ActivityResult.Success(outputData);
        }

        // No matching companies — escalate to admin
        outputData["decision"] = "no_match";
        outputData["selectionError"] = result.ErrorMessage ?? "No eligible companies";

        _logger.LogWarning(
            "CompanySelectionActivity {ActivityId}: no match, escalating to admin. Error: {Error}",
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

    private void PublishCompanyAssignedEvent(
        ActivityContext context,
        string companyIdRaw,
        string companyName,
        string assignmentMethod)
    {
        var appraisalId = WorkflowVariables.TryGetAppraisalId(context.Variables);
        if (appraisalId is null)
        {
            _logger.LogWarning(
                "CompanySelectionActivity {ActivityId}: appraisalId not in variables; skipping CompanyAssignedIntegrationEvent publish",
                context.ActivityId);
            return;
        }

        if (!Guid.TryParse(companyIdRaw, out var companyId))
        {
            _logger.LogWarning(
                "CompanySelectionActivity {ActivityId}: assignedCompanyId '{Raw}' is not a Guid; skipping publish",
                context.ActivityId, companyIdRaw);
            return;
        }

        var appraisalNumber = GetVariable<string>(context, "appraisalNumber", "");

        _outbox.Publish(new CompanyAssignedIntegrationEvent
        {
            AppraisalId = appraisalId.Value,
            CompanyId = companyId,
            CompanyName = companyName,
            AssignmentMethod = assignmentMethod,
            CompletedBy = context.WorkflowInstance.LastCompletedBy,
            AppraisalNumber = string.IsNullOrEmpty(appraisalNumber) ? null : appraisalNumber
        }, appraisalId.Value.ToString());

        _logger.LogInformation(
            "CompanySelectionActivity {ActivityId}: published CompanyAssignedIntegrationEvent for AppraisalId={AppraisalId}, CompanyId={CompanyId}",
            context.ActivityId, appraisalId.Value, companyId);
    }
}