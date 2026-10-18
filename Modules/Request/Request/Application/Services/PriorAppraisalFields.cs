using Appraisal.Contracts.Appraisals;

namespace Request.Application.Services;

/// <summary>What a save stores for the prior appraisal; <see cref="Reference"/> is the CAS lookup, when there was one.</summary>
internal sealed record PriorAppraisalFields(
    string? Number,
    decimal? Value,
    DateTime? Date,
    AppraisalReferenceResult? Reference)
{
    /// <summary>An empty Guid is "no prior"; never store it as an id.</summary>
    public static Guid? NormalizeId(Guid? id) => id == Guid.Empty ? null : id;

    /// <summary>
    /// The prior to store when an EXISTING request's detail is rebuilt by a save, for the three paths that do it
    /// (screen update, draft save, LOS resubmit). Returns the prior id as well, since a locked request keeps its own.
    /// <list type="bullet">
    /// <item>Submitted, or created by Initiate for a COLLATREV book (still a draft): which prior it follows is
    /// locked, so what was sent is only checked against the stored prior (a change, or clearing a legacy book,
    /// throws; on an Initiate draft no Detail at all is "unchanged") and the stored id, number, value and date stay. Nothing is looked up in CAS, so a prior deleted since
    /// cannot wipe them; for an Initiate draft that keeps a block-project unit's own price too.</item>
    /// <item>Any other draft: whatever was sent, resolved from CAS.</item>
    /// </list>
    /// <paramref name="legacyBookWhenUnlocked"/> is that caller's rule for a legacy book on an unlocked draft (the
    /// form never sends the number; a LOS resubmit replaces the data).
    /// </summary>
    public static async Task<(Guid? PrevId, PriorAppraisalFields Fields)> ResolveForSaveAsync(
        ISender mediator,
        Domain.Requests.Request request,
        RequestDetailDto? incoming,
        Func<(string? Number, decimal? Value, DateTime? Date)> legacyBookWhenUnlocked,
        CancellationToken cancellationToken)
    {
        var prevId = NormalizeId(incoming?.PrevAppraisalId);
        var stored = request.Detail;
        var storedFields = new PriorAppraisalFields(
            stored?.PrevAppraisalNumber, stored?.PrevAppraisalValue, stored?.PrevAppraisalDate, null);

        if (request.ArePurposeAndPriorLocked())
        {
            // On an Initiate draft, no Detail sent at all means "prior unchanged", like an omitted purpose; anything
            // else is checked. A submitted request is always checked.
            if (incoming is not null || request.HasBeenSubmitted())
                request.EnsurePriorUnchanged(
                    prevId, incoming?.PrevAppraisalNumber, LegacyPriorBook.IsCleared(incoming, stored));
            return (stored?.PrevAppraisalId, storedFields);
        }

        return (prevId, await ResolveAsync(mediator, prevId, legacyBookWhenUnlocked(), cancellationToken));
    }

    /// <summary>
    /// Resolves the number/value/date to store for an unlocked draft. A CAS appraisal (by id) supplies all three;
    /// otherwise <paramref name="legacyBook"/> does. Call before the detail is replaced.
    /// </summary>
    private static async Task<PriorAppraisalFields> ResolveAsync(
        ISender mediator,
        Guid? incomingPrevAppraisalId,
        (string? Number, decimal? Value, DateTime? Date) legacyBook,
        CancellationToken cancellationToken)
    {
        AppraisalReferenceResult? reference = null;
        if (incomingPrevAppraisalId.HasValue)
            reference = await mediator.Send(
                new GetAppraisalReferenceQuery(incomingPrevAppraisalId.Value), cancellationToken);

        return new PriorAppraisalFields(
            reference is null ? legacyBook.Number : reference.AppraisalNumber,
            reference is null ? legacyBook.Value : reference.AppraisalValue,
            reference is null ? legacyBook.Date : reference.AppraisalDate,
            reference);
    }
}
