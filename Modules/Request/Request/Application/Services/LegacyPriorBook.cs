namespace Request.Application.Services;

/// <summary>
/// Keeps a request's legacy AS400 prior book ("99A…") through a save. Only the reappraisal consumer
/// sets one (<see cref="Domain.Requests.Request.SetLegacyPriorBook"/>); it has no PrevAppraisalId to
/// resolve from, and the request form does not send it back — so an update keeps what is stored, and
/// never takes a number from the caller.
/// </summary>
internal static class LegacyPriorBook
{
    /// <summary>
    /// The stored legacy book, or nothing when staff cleared the prior appraisal. The form sends back
    /// the prior value and date it loaded but never the number, so both coming back empty is the
    /// "cleared" signal. Value and date stay as stored — they came from AS400, not from staff.
    /// </summary>
    public static (string? Number, decimal? Value, DateTime? Date) ForUpdate(
        RequestDetailDto? incoming, RequestDetail? stored)
    {
        // Staff picked a CAS prior appraisal: the two are never both set, even when that
        // appraisal's reference cannot be resolved.
        if (stored is not { PrevAppraisalId: null, PrevAppraisalNumber: not null }
            || incoming?.PrevAppraisalId is not null)
            return (null, null, null);

        // Only a signal when something was stored to send back: a book AS400 sent with neither value
        // nor date comes back empty on every save, and would otherwise lose its number on the first.
        var cleared = incoming is not null
                      && incoming.PrevAppraisalValue is null
                      && incoming.PrevAppraisalDate is null
                      && (stored.PrevAppraisalValue is not null || stored.PrevAppraisalDate is not null);

        return cleared
            ? (null, null, null)
            : (stored.PrevAppraisalNumber, stored.PrevAppraisalValue, stored.PrevAppraisalDate);
    }
}
