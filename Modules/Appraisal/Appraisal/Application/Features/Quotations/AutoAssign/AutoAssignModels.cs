namespace Appraisal.Application.Features.Quotations.AutoAssign;

public static class AutoAssignOutcome
{
    public const string Assigned = "Assigned";
    public const string Failed = "Failed";
    public const string Parked = "Parked";
    public const string Skipped = "Skipped";

    /// <summary>SystemConfiguration go-live switch; mirrors CompanySelectionActivity.CompanyAssignmentEnabledKey.</summary>
    public const string ExternalAssignmentEnabledKey = "ExternalCompanyAssignmentEnabled";
}

/// <summary>
/// Stable codes stored in <c>AutoAssignReasonCode</c> so the UI can translate the reason. The English message
/// stays as the fallback and detail. A plain Assigned result has no code.
/// </summary>
public static class AutoAssignReason
{
    // Classifier
    public const string Deleted = "deleted";
    public const string WorkflowEnded = "workflowEnded";
    public const string NoAssignment = "noAssignment";
    public const string AssignmentNotReset = "assignmentNotReset";
    public const string NotPending = "notPending";
    public const string ConstructionInspection = "constructionInspection";
    public const string NotAtAssignment = "notAtAssignment";
    public const string TaskHeld = "taskHeld";
    public const string DocumentFollowupOpen = "documentFollowupOpen";
    public const string WinnerUnavailable = "winnerUnavailable";
    public const string Unpriced = "unpriced";

    // Handler
    public const string Stopped = "stopped";
    public const string UnexpectedStop = "unexpectedStop";
    public const string StateUnreadable = "stateUnreadable";
    public const string StaffIneligible = "staffIneligible";
    public const string StaffValidationFailed = "staffValidationFailed";
    public const string AssignRejected = "assignRejected";
    public const string AssignFailed = "assignFailed";
    public const string NoMatch = "noMatch";
    public const string WorkflowElsewhere = "workflowElsewhere";
    public const string Unverified = "unverified";
}

/// <summary>Admin-picked follow-up staff for one appraisal. Appraisals without an entry use round robin.</summary>
public record AutoAssignStaff(Guid AppraisalId, string StaffId);

public record AutoAssignRequest(IReadOnlyList<AutoAssignStaff>? Staff = null);

public record AutoAssignItemResult(Guid AppraisalId, string Result, string? Message, string? ReasonCode = null);
