namespace Integration.Application.Services;

/// <summary>
/// Maps destination workflow activity IDs to the external-facing status sent via webhook.
/// Activities not present are skipped (no webhook sent).
/// </summary>
public static class IntegrationStatusMap
{
    private static readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ext-appraisal-assignment"]   = "IN_PROGRESS",
        ["int-appraisal-execution"]    = "IN_PROGRESS",
        ["appraisal-initiation-check"] = "IN_PROGRESS",
        ["appraisal-assignment"]       = "IN_PROGRESS",
    };

    /// <summary>Returns the external status for the given activity, or null if unmapped.</summary>
    public static string? Map(string? activityId) =>
        activityId is not null && _map.TryGetValue(activityId, out var status) ? status : null;
}
