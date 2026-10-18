namespace Request.Application.Services;

/// <summary>
/// Carries a request's legacy AS400 prior book ("99A…") through a save. The form never sends the number
/// back, so a UI update keeps what is stored and never takes a number from the caller (<see cref="ForUpdate"/>).
/// An Integration resubmit replaces the request data, so the 99A number LOS sends now is the book
/// (<see cref="ForResubmit"/>).
/// </summary>
internal static class LegacyPriorBook
{
    /// <summary>
    /// The stored legacy book, or nothing when staff cleared the prior appraisal. The form sends back
    /// the prior value and date it loaded but never the number, so both coming back empty is the
    /// "cleared" signal. Value and date stay as stored — they came from AS400, not from staff.
    /// </summary>
    public static (string? Number, decimal? Value, DateTime? Date) ForUpdate(
        string? purpose, RequestDetailDto? incoming, RequestDetail? stored)
    {
        // A new appraisal (07) has no prior book. Without this a book stored with neither value nor date
        // could never be cleared: the form sends back exactly those nulls whether or not staff cleared it.
        if (purpose == Domain.Requests.Request.NewAppraisalPurpose)
            return (null, null, null);

        // Staff picked a CAS prior appraisal: the two are never both set, even when that
        // appraisal's reference cannot be resolved.
        if (!HasStoredLegacyBook(incoming, stored))
            return (null, null, null);

        return IsCleared(incoming, stored)
            ? (null, null, null)
            : (stored!.PrevAppraisalNumber, stored.PrevAppraisalValue, stored.PrevAppraisalDate);
    }

    /// <summary>A legacy book is stored (a number and no CAS id) and no CAS prior was picked over it.</summary>
    private static bool HasStoredLegacyBook(RequestDetailDto? incoming, RequestDetail? stored) =>
        stored is { PrevAppraisalId: null, PrevAppraisalNumber: not null }
        && PriorAppraisalFields.NormalizeId(incoming?.PrevAppraisalId) is null;

    /// <summary>
    /// Staff cleared a stored legacy book: it has a number and no CAS id, no CAS prior was picked, and the
    /// form sent back neither value nor date. Only a signal when something was stored to send back: a book
    /// AS400 sent with neither value nor date comes back empty on every save, and would otherwise lose its
    /// number on the first.
    /// </summary>
    public static bool IsCleared(RequestDetailDto? incoming, RequestDetail? stored) =>
        HasStoredLegacyBook(incoming, stored)
        && incoming is not null
        // LOS names its book by number; only the form omits it, so a number sent is not a clear.
        && string.IsNullOrWhiteSpace(incoming.PrevAppraisalNumber)
        && incoming.PrevAppraisalValue is null
        && incoming.PrevAppraisalDate is null
        && (stored!.PrevAppraisalValue is not null || stored.PrevAppraisalDate is not null);

    /// <summary>
    /// A number with no CAS appraisal behind it that the system accepts as a prior book: a real legacy AS400 book
    /// (99A…), or the book a periodical reappraisal draft was created for (<c>Request.ReappraisalBookNumber</c>,
    /// which need not be a 99A). The submit guard's rule. Free text typed by staff is neither.
    /// </summary>
    public static bool IsLegacyBook(string? number, string? reappraisalBookNumber) =>
        Domain.Requests.Request.IsLegacyPriorBook(number)
        || (!string.IsNullOrWhiteSpace(reappraisalBookNumber)
            && Domain.Requests.Request.NormalizeBookNumber(number)
                == Domain.Requests.Request.NormalizeBookNumber(reappraisalBookNumber));

    /// <summary>
    /// The legacy book an UNLOCKED resubmit carries: the 99A number LOS sent with no id, with the value and date it
    /// sent. Anything else is no legacy book — LOS omitting the prior clears it. (A locked request never gets here:
    /// it keeps its stored prior.)
    /// </summary>
    public static (string? Number, decimal? Value, DateTime? Date) ForResubmit(RequestDetailDto incoming) =>
        PriorAppraisalFields.NormalizeId(incoming.PrevAppraisalId) is null
        && Domain.Requests.Request.IsLegacyPriorBook(incoming.PrevAppraisalNumber)
            ? (Domain.Requests.Request.NormalizeBookNumber(incoming.PrevAppraisalNumber), incoming.PrevAppraisalValue, incoming.PrevAppraisalDate)
            : (null, null, null);
}
