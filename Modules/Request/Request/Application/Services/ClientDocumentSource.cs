namespace Request.Application.Services;

/// <summary>
/// The Source of a document row sent by a client (the request page, LOS): "REQUEST" (its own upload) or "PREV"
/// (carried from the previous appraisal), in any case and with stray spaces; the canonical upper-case value is
/// stored. Anything else becomes "REQUEST". "FOLLOWUP" is stamped by the server
/// only (RequestDocumentAttacher, and the Integration follow-up resubmit), so a client can never create such a
/// row or turn one into it; the one exception is a title's documents being re-created from the payload when its
/// collateral type changes, where the client echoes the label of a stored FOLLOWUP row
/// (<paramref name="echoesStoredFollowUp"/>).
/// Applied where every caller passes through — CreateRequestService and RequestSyncService — so LOS and the
/// request page are both covered.
/// </summary>
internal static class ClientDocumentSource
{
    public const string FollowUp = "FOLLOWUP";

    public static string Normalize(string? source, bool echoesStoredFollowUp = false)
    {
        var label = source?.Trim();
        if (string.Equals(label, "PREV", StringComparison.OrdinalIgnoreCase)) return "PREV";
        if (echoesStoredFollowUp && IsFollowUp(label)) return FollowUp;
        return "REQUEST"; // REQUEST in any spelling, and everything else
    }

    /// <summary>
    /// A stored Source is a follow-up label whatever its case: rows written by older builds may read "FollowUp",
    /// and they must keep their label (compare with this, never with ==).
    /// </summary>
    public static bool IsFollowUp(string? storedSource) =>
        string.Equals(storedSource?.Trim(), FollowUp, StringComparison.OrdinalIgnoreCase);
}
