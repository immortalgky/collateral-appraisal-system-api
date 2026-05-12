namespace Integration.Application.Services;

/// <summary>
/// Maps internal <c>AppraisalStatus</c> code values to the external webhook bucket
/// sent to subscribers as <c>status</c> in the <c>APPRAISAL_STATUS_CHANGED</c> payload.
/// Unknown status codes return <c>null</c> and are suppressed by the consumer.
/// </summary>
public static class IntegrationStatusMap
{
    private static readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Submitted"]   = "IN_PROGRESS",
        ["Pending"]     = "IN_PROGRESS",
        ["Assigned"]    = "IN_PROGRESS",
        ["InProgress"]  = "IN_PROGRESS",
        ["UnderReview"] = "IN_PROGRESS",
        ["Completed"]   = "COMPLETED",
        ["Cancelled"]   = "CANCELLED",
    };

    /// <summary>Returns the external bucket for the given internal status, or null if unmapped.</summary>
    public static string? Map(string? statusCode) =>
        statusCode is not null && _map.TryGetValue(statusCode, out var bucket) ? bucket : null;
}
