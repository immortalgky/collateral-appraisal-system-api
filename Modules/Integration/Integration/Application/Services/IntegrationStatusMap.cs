namespace Integration.Application.Services;

/// <summary>
/// Maps completed workflow activity IDs to the external-facing status the appraisal
/// is now in AFTER that activity finishes. Activities not present are skipped (no webhook).
///
/// Mapping (each entry = the state the appraisal enters after this activity completes):
///   ext-appraisal-assignment    → IN_PROGRESS     : external company execution phase begins
///   appraisal-book-verification → IN_PROGRESS     : internal check/verification phase begins
///   int-appraisal-execution     → IN_PROGRESS     : internal check phase begins
///   pending-meeting             → PENDING_APPROVAL: meeting concluded, committee approval phase begins
///   pending-approval            → COMPLETED       : committee approved, workflow ends
/// </summary>
public static class IntegrationStatusMap
{
    private static readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ext-appraisal-assignment"] = "IN_PROGRESS",
        ["appraisal-book-verification"] = "IN_PROGRESS",
        ["int-appraisal-execution"] = "IN_PROGRESS",
        ["pending-meeting"] = "PENDING_APPROVAL",
        ["pending-approval"] = "COMPLETED",
    };

    /// <summary>Returns the external status for the given activity, or null if unmapped.</summary>
    public static string? Map(string? activityId) =>
        activityId is not null && _map.TryGetValue(activityId, out var status) ? status : null;
}
