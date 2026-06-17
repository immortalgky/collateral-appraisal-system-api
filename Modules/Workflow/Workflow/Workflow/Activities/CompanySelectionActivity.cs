using Auth.Domain.Companies;
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
    private readonly ICompanyRepository _companyRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly ILogger<CompanySelectionActivity> _logger;

    public CompanySelectionActivity(
        ICompanyRoundRobinService companyRoundRobinService,
        ICompanyRepository companyRepository,
        IDateTimeProvider dateTimeProvider,
        IIntegrationEventOutbox outbox,
        ILogger<CompanySelectionActivity> logger)
    {
        _companyRoundRobinService = companyRoundRobinService;
        _companyRepository = companyRepository;
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
        // Round-robin pool scope comes from the banking segment ("Retail"/"IBG"), set at workflow
        // start by RequestSubmittedIntegrationEventConsumer. The legacy "loanType" variable is
        // declared in appraisal-workflow.json but never assigned, so reading it always yielded ""
        // and the Retail/IBG pools were never matched. Local name kept as loanType so the replay
        // guard, assignedCompanyLoanType output, and SelectCompanyAsync(...) calls are unchanged.
        var loanType = GetVariable<string>(context, "bankingSegment", "");
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
            if (!await IsAssignableAsync(forceCompanyId, cancellationToken))
                return EscalateNotAssignable(context, outputData, forceCompanyId, "Forced");

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

            // Block a manually/quotation-selected company that is outside its MOU approval window (e.g.
            // an MOU that lapsed between sending the quotation and finalising it). Escalate to admin.
            if (!await IsAssignableAsync(companyId, cancellationToken))
                return EscalateNotAssignable(context, outputData, companyId, selectionMethod);

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
        SetNoMatch(outputData, result.ErrorMessage ?? "No eligible companies");

        _logger.LogWarning(
            "CompanySelectionActivity {ActivityId}: no match, escalating to admin. Error: {Error}",
            context.ActivityId, result.ErrorMessage);

        return ActivityResult.Success(outputData);
    }

    // Mark the selection as needing admin review. Clears any company id so the no_match state can't
    // carry a stale assignment forward (workflow output merges into Variables; on replay/routeback a
    // previously-selected company id would otherwise linger under a no_match decision).
    private static void SetNoMatch(Dictionary<string, object> outputData, string error)
    {
        outputData["decision"] = "no_match";
        outputData["selectionError"] = error;
        outputData["assignedCompanyId"] = "";
        outputData["assignedCompanyName"] = "";
    }

    // True only if the company exists and is within its MOU approval window — the same rule the
    // round-robin path applies, so manual/quotation/forced selections can't route to an expired company.
    private async Task<bool> IsAssignableAsync(string companyIdRaw, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(companyIdRaw, out var id)) return false;
        var company = await _companyRepository.GetByIdAsync(id, cancellationToken);
        return company is not null && company.IsAssignable(_dateTimeProvider.ApplicationNow);
    }

    // Selected company can't take the assignment (outside its MOU window) — escalate to admin review
    // via the activity's no_match transition rather than publishing an assignment.
    private ActivityResult EscalateNotAssignable(
        ActivityContext context,
        Dictionary<string, object> outputData,
        string companyId,
        string method)
    {
        SetNoMatch(outputData, "Selected company is not currently assignable (outside its MOU approval window).");

        _logger.LogWarning(
            "CompanySelectionActivity {ActivityId}: {Method} company {CompanyId} is outside its MOU window; escalating to admin",
            context.ActivityId, method, companyId);

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