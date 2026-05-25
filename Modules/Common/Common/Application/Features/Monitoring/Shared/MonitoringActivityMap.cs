namespace Common.Application.Features.Monitoring.Shared;

/// <summary>
/// Maps monitoring layer permission suffixes to workflow activity IDs.
///
/// Semantic: the layer suffix names the STAGE OF WORK BEING MONITORED — not the monitor's role.
/// A user holding "MONITORING:PENDING_INTERNAL:CHECKER" sees rows whose ActivityId is the
/// check stage, regardless of that user's own workflow role.
///
/// Internal activities (bank-side appraisal pipeline):
///   STAFF    → int-appraisal-execution, appraisal-book-verification
///              (both are bank-staff TaskActivity work)
///   CHECKER  → int-appraisal-check
///   VERIFIER → int-appraisal-verification
///   APPROVER → pending-approval  (committee ApprovalActivity)
///   ADMIN    → appraisal-assignment  (parallel to External ADMIN)
///
/// External activities (external appraisal company executing the appraisal):
///   STAFF    → ext-appraisal-execution
///   CHECKER  → ext-appraisal-check
///   VERIFIER → ext-appraisal-verification
///   ADMIN    → ext-appraisal-assignment  (admin assigns external company)
///
/// Followup activities (the Pending Followup screen — shared list used by 4 handlers):
///   appraisal-initiation, appraisal-initiation-check, provide-additional-documents
///
/// Layer keys (dictionary keys) match the permission code suffix after the screen prefix
/// (e.g. for MONITORING:PENDING_INTERNAL:CHECKER the suffix "CHECKER" is the layer key).
/// The maps are static — activity IDs change with workflow definitions, not DB config (Decision D12).
/// </summary>
public static class MonitoringActivityMap
{
    /// <summary>Internal screen: permission suffix → activity IDs visible to that layer.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Internal =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["STAFF"] = ["int-appraisal-execution", "appraisal-book-verification"],
            ["CHECKER"] = ["int-appraisal-check"],
            ["VERIFIER"] = ["int-appraisal-verification"],
            ["APPROVER"] = ["pending-approval"],
            ["ADMIN"] = ["appraisal-assignment"]
        };

    /// <summary>External screen: permission suffix → activity IDs visible to that layer.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> External =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["STAFF"] = ["ext-appraisal-execution"],
            ["CHECKER"] = ["ext-appraisal-check"],
            ["VERIFIER"] = ["ext-appraisal-verification"],
            ["ADMIN"] = ["ext-appraisal-assignment"]
        };

    /// <summary>
    /// Followup screen: shared activity ID list. Referenced by GetPendingFollowups* handlers
    /// and the Followup branch of GetTopBreachesQueryHandler.
    /// </summary>
    public static readonly string[] Followup =
    [
        "appraisal-initiation",
        "appraisal-initiation-check",
        "provide-additional-documents"
    ];
}
