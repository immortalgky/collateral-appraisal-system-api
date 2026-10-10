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
    /// Resolves the number/value/date to store, shared by every path that rebuilds a request's detail. A CAS
    /// appraisal (by id) supplies all three; otherwise <paramref name="legacyBook"/> does. A periodical
    /// reappraisal keeps the prior value/date Initiate set (a block-project unit's are the unit's, not the
    /// whole project's) while its prior appraisal is unchanged. Call before the detail is replaced.
    /// </summary>
    public static async Task<PriorAppraisalFields> ResolveAsync(
        ISender mediator,
        Domain.Requests.Request request,
        Guid? incomingPrevAppraisalId,
        (string? Number, decimal? Value, DateTime? Date) legacyBook,
        CancellationToken cancellationToken)
    {
        var unitPrior = request.ReappraisalBookNumber is not null
                        && request.Detail?.PrevAppraisalId is { } storedPrev
                        && incomingPrevAppraisalId == storedPrev
            ? request.Detail
            : null;

        AppraisalReferenceResult? reference = null;
        if (incomingPrevAppraisalId.HasValue)
            reference = await mediator.Send(
                new GetAppraisalReferenceQuery(incomingPrevAppraisalId.Value), cancellationToken);

        return new PriorAppraisalFields(
            reference is null ? legacyBook.Number : reference.AppraisalNumber,
            unitPrior is not null ? unitPrior.PrevAppraisalValue : reference is null ? legacyBook.Value : reference.AppraisalValue,
            unitPrior is not null ? unitPrior.PrevAppraisalDate : reference is null ? legacyBook.Date : reference.AppraisalDate,
            reference);
    }
}
