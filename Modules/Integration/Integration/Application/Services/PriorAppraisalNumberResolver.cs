using Request.Contracts.Requests.Dtos;
using Shared.Exceptions;

namespace Integration.Application.Services;

/// <summary>
/// External callers reference the prior appraisal by its number (SurveyNo), not our internal GUID.
/// Resolves it to PrevAppraisalId for every purpose, rejecting — naming the number — when it is
/// missing or not yet Completed. An explicit GUID wins. A legacy AS400 "99A…" number has no appraisal
/// in CAS, so it is left on the detail for the caller to record as a legacy prior book. Whether the
/// purpose needs, forbids or allows a prior appraisal is PriorAppraisalSubmissionGuard's call, not this;
/// for a new appraisal (07) nothing is looked up, so the guard's "must not reference a prior" error wins.
/// </summary>
internal static class PriorAppraisalNumberResolver
{
    // Cross-module string contract for AppraisalStatus.Completed (mirrors PriorAppraisalSubmissionGuard).
    private const string CompletedStatus = "Completed";

    public static async Task<RequestDetailDto?> ResolveAsync(
        IAppraisalLookupService appraisalLookup,
        string? purpose,
        RequestDetailDto? detail,
        CancellationToken cancellationToken,
        string? recordedBookNumber = null)
    {
        if (purpose == Request.Domain.Requests.Request.NewAppraisalPurpose)
            return detail;

        // An empty Guid is "no id".
        if (detail?.PrevAppraisalId == Guid.Empty)
            detail = detail with { PrevAppraisalId = null };

        if (detail is not { PrevAppraisalId: null } || string.IsNullOrWhiteSpace(detail.PrevAppraisalNumber))
            return detail;

        var number = detail.PrevAppraisalNumber.Trim();

        if (Request.Domain.Requests.Request.IsLegacyPriorBook(number))
            return detail with { PrevAppraisalNumber = Request.Domain.Requests.Request.NormalizeBookNumber(number) };

        // A resubmit of a periodical reappraisal draft: the number is the book the system recorded on it (it need
        // not be a 99A and has no CAS appraisal), so there is nothing to look up. The guard accepts it as that book.
        if (!string.IsNullOrWhiteSpace(recordedBookNumber)
            && Request.Domain.Requests.Request.NormalizeBookNumber(number)
                == Request.Domain.Requests.Request.NormalizeBookNumber(recordedBookNumber))
            return detail with { PrevAppraisalNumber = Request.Domain.Requests.Request.NormalizeBookNumber(number) };

        // AS400's spelling (spaces, case, a leading 'B') is read the way the reappraisal flow reads it.
        var prior = await appraisalLookup.ResolvePriorAppraisalByNumberAsync(
            Request.Domain.Requests.Request.NormalizeBookNumber(number)!, cancellationToken);
        if (prior is null)
            throw new BadRequestException($"No appraisal found for AppraisalNumber '{number}'.");
        if (!string.Equals(prior.Status, CompletedStatus, StringComparison.Ordinal))
            throw new BadRequestException(
                $"The prior appraisal '{number}' must be completed before this request can be submitted.");

        return detail with { PrevAppraisalId = prior.Id };
    }

    /// <summary>The legacy "99A…" number left on the detail by <see cref="ResolveAsync"/>, if any.</summary>
    public static string? LegacyNumber(RequestDetailDto? detail) =>
        detail is { PrevAppraisalId: null } && Request.Domain.Requests.Request.IsLegacyPriorBook(detail.PrevAppraisalNumber)
            ? Request.Domain.Requests.Request.NormalizeBookNumber(detail.PrevAppraisalNumber)
            : null;
}
