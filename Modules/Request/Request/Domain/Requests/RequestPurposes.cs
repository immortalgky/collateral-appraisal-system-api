namespace Request.Domain.Requests;

/// <summary>Meaning of the request purpose codes that more than one place has to agree on.</summary>
internal static class RequestPurposes
{
    /// <summary>
    /// Construction inspection: purposes 06 and 11 (one group), both of which build a Progressive appraisal.
    /// The one definition shared by the submit guard, the submit event's appraisal type and the request page.
    /// </summary>
    public static bool IsConstructionInspection(string? purpose) => purpose is "06" or "11";
}
