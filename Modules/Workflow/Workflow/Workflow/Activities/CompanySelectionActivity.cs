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
    private readonly ILogger<CompanySelectionActivity> _logger;

    public CompanySelectionActivity(
        ICompanyRoundRobinService companyRoundRobinService,
        ILogger<CompanySelectionActivity> logger)
    {
        _companyRoundRobinService = companyRoundRobinService;
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

        var outputData = new Dictionary<string, object>
        {
            ["selectionMethod"] = selectionMethod,
            ["selectedAt"] = DateTime.UtcNow
        };

        if (selectionMethod == "manual")
        {
            var companyId = GetVariable<string>(context, "selectedCompanyId", "");
            var companyName = GetVariable<string>(context, "selectedCompanyName", "");

            if (string.IsNullOrEmpty(companyId))
            {
                _logger.LogWarning("CompanySelectionActivity {ActivityId}: manual selection but no company selected",
                    context.ActivityId);
                return ActivityResult.Failed("No company selected for manual assignment");
            }

            outputData["assignedCompanyId"] = companyId;
            outputData["assignedCompanyName"] = companyName;
            outputData["assignmentMethod"] = "Manual";
            outputData["decision"] = "company_selected";

            _logger.LogInformation(
                "CompanySelectionActivity {ActivityId}: manually selected company {CompanyName} ({CompanyId})",
                context.ActivityId, companyName, companyId);

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

        // Round-robin selection, filtered by LoanType if available
        var result = string.IsNullOrEmpty(loanType)
            ? await _companyRoundRobinService.SelectCompanyAsync(cancellationToken)
            : await _companyRoundRobinService.SelectCompanyAsync(loanType, cancellationToken);

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
}