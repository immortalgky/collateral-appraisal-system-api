using Auth.Domain.Companies;
using Shared.Configuration;
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
    /// <summary>
    /// SystemConfiguration key gating whether an appraisal may be assigned to an external company
    /// INSIDE the system at all — by round-robin, by an admin's manual pick, through a quotation
    /// winner, or by Construction-Inspection carry-over.
    ///
    /// Set to "false" for the phase-1 go-live window, during which the bank engages appraisal
    /// companies outside CAS; such cases are escalated to admin review instead, to be routed
    /// internally or recorded as an off-system engagement (EXTO). Flip it to "true" through the
    /// SystemConfiguration admin API to resume normal assignment — no redeploy and no new workflow
    /// definition version.
    /// </summary>
    public const string CompanyAssignmentEnabledKey = "ExternalCompanyAssignmentEnabled";

    private readonly ICompanyRoundRobinService _companyRoundRobinService;
    private readonly ICompanyRepository _companyRepository;
    private readonly ISystemConfigurationReader _configReader;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly ILogger<CompanySelectionActivity> _logger;

    public CompanySelectionActivity(
        ICompanyRoundRobinService companyRoundRobinService,
        ICompanyRepository companyRepository,
        ISystemConfigurationReader configReader,
        IDateTimeProvider dateTimeProvider,
        IIntegrationEventOutbox outbox,
        ILogger<CompanySelectionActivity> logger)
    {
        _companyRoundRobinService = companyRoundRobinService;
        _companyRepository = companyRepository;
        _configReader = configReader;
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

        // ── Go-live switch: no company may be engaged INSIDE the system at all ────────────────
        // Deliberately ahead of every selection branch, so it blocks round-robin, admin Manual,
        // Quotation and the Construction-Inspection carry-over alike. The bank engages appraisal
        // companies outside CAS during phase 1, so an in-system assignment to a company is wrong
        // however it was arrived at — an admin choosing one explicitly is no more correct than the
        // system picking one. Such cases escalate to appraisal-assignment via the existing
        // company-no-match transition, where the admin routes them Internal or records the
        // off-system engagement (EXTO).
        //
        // The one exemption is a case that ALREADY holds a company: that engagement predates the
        // switch and must not be torn up when this activity replays.
        //
        // This does NOT loop, because the admin screen stops offering "Route to External" while the
        // switch is off — see AdministrationPage. The guard here is the backing enforcement for any
        // caller that bypasses the UI.
        //
        // The exemption only covers REUSING the engagement the case already holds, judged on the company this
        // pass resolves to (below): the fresh selectedCompanyId on Manual/Quotation (which wins over assignedCompanyId),
        // else the forced (construction inspection) company when nobody chose, else on Manual/Quotation the assigned
        // one, else on round robin the assigned one only when the replay guard fires. A different company, or a fresh round-robin pick, is a
        // NEW engagement and the switch applies to it.
        // Known gap (accepted): assignedCompanyId survives an ext cancel / return, so an admin re-picking that
        // same company is treated as reuse and stays exempt.
        var assignedCompanyId = GetVariable<string>(context, "assignedCompanyId", "");
        var assignedCompanyName = GetVariable<string>(context, "assignedCompanyName", "");
        var forceCompanyId = GetVariable<string>(context, "forceCompanyId", "");
        var isManual = string.Equals(selectionMethod, "manual", StringComparison.OrdinalIgnoreCase);
        var isQuotation = string.Equals(selectionMethod, "Quotation", StringComparison.OrdinalIgnoreCase);
        var selectedCompanyId = isManual || isQuotation ? GetVariable<string>(context, "selectedCompanyId", "") : "";
        // Priority: an explicit selectedCompanyId (the admin's manual pick, or the quotation winner) wins; only when
        // none was written (round robin / auto routing, or Manual/Quotation with an empty selection) is the company
        // forced from the prior appraisal. selectedCompanyId is already "" outside Manual/Quotation.
        var isForced = Guid.TryParse(forceCompanyId, out _) && string.IsNullOrEmpty(selectedCompanyId);
        // Replay guard: reuse the previously selected company if the selection condition (loanType) has not changed.
        var replaysAssignedCompany = !isForced && !isManual && !isQuotation
                                     && !string.IsNullOrEmpty(assignedCompanyId)
                                     && GetVariable<string>(context, "assignedCompanyLoanType", "") == loanType;

        // The effective company, resolved once. Id and name are taken as a pair so a name never comes from
        // a different company. Empty means round robin picks a new one.
        var (companyId, companyName) =
            isForced ? (forceCompanyId, GetVariable<string>(context, "forceCompanyName", ""))
            : !string.IsNullOrEmpty(selectedCompanyId) ? (selectedCompanyId, GetVariable<string>(context, "selectedCompanyName", ""))
            : isManual || isQuotation || replaysAssignedCompany ? (assignedCompanyId, assignedCompanyName)
            : ("", "");
        var keepsAssignedCompany = !string.IsNullOrEmpty(assignedCompanyId) && SameCompany(companyId, assignedCompanyId);

        if (!keepsAssignedCompany
            && !await _configReader.GetBoolAsync(CompanyAssignmentEnabledKey, defaultValue: true, cancellationToken))
        {
            SetNoMatch(outputData,
                "Assigning an appraisal to an external company inside the system is currently " +
                "disabled. Route the case to an internal appraiser, or record an engagement " +
                "arranged outside the system.");

            _logger.LogInformation(
                "CompanySelectionActivity {ActivityId}: in-system company assignment disabled via '{Key}' " +
                "(method '{Method}'); escalating to admin",
                context.ActivityId, CompanyAssignmentEnabledKey, selectionMethod);

            return ActivityResult.Success(outputData);
        }

        // Construction Inspection: nobody chose, so the company is forced from the picked prior appraisal.
        // Short-circuit selection entirely — no round-robin, no exclusion check.
        if (isForced)
        {
            var company = await GetAssignableCompanyAsync(companyId, cancellationToken);
            if (company is null)
            {
                // The forced company cannot take it (e.g. its MOU expired), so the case goes to the admin — who must
                // then be free to pick anyone. forceCompanyId lives in the instance variables for good, so without
                // this the admin's next EXT + RoundRobin (no selectedCompanyId) would be forced to the same company
                // and escalate again, forever. Spending the force here lets the admin's choice (RoundRobin, or a
                // manual pick) proceed on the normal rules.
                outputData["forceCompanyId"] = "";
                outputData["forceCompanyName"] = "";
                return EscalateNotAssignable(context, outputData, companyId, "Forced");
            }

            // A missing name is filled from the forced company itself.
            if (string.IsNullOrEmpty(companyName))
                companyName = company.Name;

            outputData["assignedCompanyId"] = companyId;
            outputData["assignedCompanyName"] = companyName;
            outputData["assignmentMethod"] = "Forced";
            outputData["decision"] = "company_selected";

            _logger.LogInformation(
                "CompanySelectionActivity {ActivityId}: forced company {CompanyName} ({CompanyId}) for Construction Inspection",
                context.ActivityId, companyName, companyId);

            PublishCompanyAssignedEvent(context, companyId, companyName, "Forced");
            return ActivityResult.Success(outputData);
        }

        if (isManual || isQuotation)
        {
            // companyId is the admin's fresh pick (selectedCompanyId, written by the Assign command on every
            // resume), else the assigned one. assignedCompanyId is this activity's own output from an earlier
            // pass, so on re-entry (route back, cancel, earlier round robin) it is stale and must not override
            // a new pick.
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
            var company = await GetAssignableCompanyAsync(companyId, cancellationToken);
            if (company is null)
                return EscalateNotAssignable(context, outputData, companyId, selectionMethod);

            // A missing name is filled from the chosen company itself, never from another one.
            if (string.IsNullOrEmpty(companyName))
                companyName = company.Name;

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

        if (replaysAssignedCompany)
        {
            outputData["assignedCompanyId"] = companyId;
            outputData["assignedCompanyName"] = companyName;
            outputData["assignmentMethod"] = selectionMethod;
            outputData["assignedCompanyLoanType"] = loanType;
            outputData["decision"] = "company_selected";

            _logger.LogInformation(
                "CompanySelectionActivity {ActivityId}: replaying — reusing previously selected company {CompanyName} ({CompanyId})",
                context.ActivityId, companyName, companyId);

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

    // The company when it exists and is within its MOU window — the same rule the round-robin path applies,
    // so manual/quotation/forced selections can't route to an expired company — else null.
    private async Task<Company?> GetAssignableCompanyAsync(string companyIdRaw, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(companyIdRaw, out var id)) return null;
        var company = await _companyRepository.GetByIdAsync(id, cancellationToken);
        return company is not null && company.IsAssignable(_dateTimeProvider.ApplicationNow) ? company : null;
    }

    // Guid text compared as Guids (case/format-insensitive); anything unparsable falls back to ordinal-ignore-case.
    private static bool SameCompany(string a, string b) =>
        Guid.TryParse(a, out var ga) && Guid.TryParse(b, out var gb)
            ? ga == gb
            : string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

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