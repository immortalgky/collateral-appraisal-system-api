using System.Globalization;
using Appraisal.Contracts.Appraisals;
using Shared.Exceptions;

namespace Request.Application.Services;

/// <summary>
/// Submit-time gate for the prior appraisal a request refers to. The single source of the rule, shared
/// by the screen and Integration:
///   - purposes 02,03,04,05,06,08,09,11,12,13 require a prior appraisal;
///   - purpose 07 (new appraisal) must not carry one;
///   - any other purpose may carry one;
///   - a prior appraisal given by id must exist and be Completed;
///   - a prior book given by number only (no id) counts as a prior only when it is a real legacy AS400
///     book ("99A…", see <see cref="Domain.Requests.Request.IsLegacyPriorBook"/>, kept by
///     <see cref="Domain.Requests.Request.SetLegacyPriorBook"/>) — nothing in CAS to check, so it is accepted,
///     except for 06, 11 and 12: Progressive/CI and Appeal take copy/fee from the CAS appraisal, so those
///     need the id. A periodical reappraisal draft's own book (<c>Request.ReappraisalBookNumber</c>, set by the
///     system; it need not be a 99A) counts too: it is the draft's stored prior, which staff cannot change or
///     clear (the request is locked from creation). Any other number alone is free text: it does not
///     satisfy a purpose that requires a prior (same 400 as none), and on an optional purpose it is allowed
///     as is, with no lookup;
///   - purposes 06 and 11 (construction inspection, one group) additionally need the chain of the prior to have room
///     for another inspection: construction not already 100% complete, no later inspection on the chain, and no
///     other submitted inspection request still open on it (<see cref="GetConstructionChainStatusQuery"/>). The
///     400 carries the reason as <c>errorCode</c> (a <see cref="ConstructionChainBlockReasons"/> value).
///
/// Appeal/Progressive/reappraisal flows resolve company/copy/fee from the prior appraisal, so without a
/// sound one they would silently degrade. Throws BadRequestException (HTTP 400) so the failure surfaces
/// in front of the user before any workflow/appraisal is created.
/// </summary>
internal static class PriorAppraisalSubmissionGuard
{
    private static readonly HashSet<string> PriorAppraisalRequiredPurposes =
        new(StringComparer.Ordinal) { "02", "03", "04", "05", "06", "08", "09", "11", "12", "13" };

    // Continue a CAS appraisal (copy, company, fee): a number alone is not enough.
    private static readonly HashSet<string> PriorAppraisalIdRequiredPurposes =
        new(StringComparer.Ordinal) { "06", "11", "12" };

    // Construction inspection: Progressive appraisals, whichever of the two purposes asked for them.
    private static readonly HashSet<string> ConstructionInspectionPurposes =
        new(StringComparer.Ordinal) { "06", "11" };

    // Cross-module string contract for AppraisalStatus.Completed (Appraisal.Domain is not referenced here).
    private const string CompletedStatus = "Completed";

    public static async Task EnsureValidAsync(
        string? purpose,
        Guid? prevAppraisalId,
        string? prevAppraisalNumber,
        ISender mediator,
        CancellationToken cancellationToken,
        AppraisalReferenceResult? resolvedReference = null,
        string? reappraisalBookNumber = null)
    {
        if (purpose is null)
            return;

        prevAppraisalId = PriorAppraisalFields.NormalizeId(prevAppraisalId);

        var hasPrior = prevAppraisalId.HasValue || !string.IsNullOrWhiteSpace(prevAppraisalNumber);

        if (purpose == Domain.Requests.Request.NewAppraisalPurpose)
        {
            if (hasPrior)
                throw new BadRequestException("A new appraisal (purpose 07) must not reference a prior appraisal.");
            return;
        }

        if (!hasPrior)
        {
            if (PriorAppraisalRequiredPurposes.Contains(purpose))
                throw new BadRequestException("A prior appraisal is required for this request purpose.");
            return;
        }

        if (!prevAppraisalId.HasValue)
        {
            var legacyBook = LegacyPriorBook.IsLegacyBook(prevAppraisalNumber, reappraisalBookNumber);

            if (PriorAppraisalRequiredPurposes.Contains(purpose) && !legacyBook)
                throw new BadRequestException("A prior appraisal is required for this request purpose.");

            if (PriorAppraisalIdRequiredPurposes.Contains(purpose))
                throw new BadRequestException(
                    "The prior appraisal for this request purpose must be an appraisal in this system.");

            return; // legacy 99A book, or free text on an optional purpose: number only
        }

        // resolvedReference: the caller already looked this id up, so don't ask twice.
        var prior = resolvedReference
                    ?? await mediator.Send(new GetAppraisalReferenceQuery(prevAppraisalId.Value), cancellationToken);

        if (prior is null)
            throw new BadRequestException($"The referenced prior appraisal ({(string.IsNullOrWhiteSpace(prevAppraisalNumber) ? prevAppraisalId : prevAppraisalNumber)}) was not found.");

        if (!string.Equals(prior.Status, CompletedStatus, StringComparison.Ordinal))
            throw new BadRequestException(
                $"The referenced prior appraisal {prior.AppraisalNumber ?? prevAppraisalId.ToString()} " +
                "must be completed before this request can be submitted.");

        if (ConstructionInspectionPurposes.Contains(purpose))
            await EnsureChainAllowsInspectionAsync(
                prevAppraisalId.Value, prior.AppraisalNumber ?? prevAppraisalNumber, mediator, cancellationToken);
    }

    private static async Task EnsureChainAllowsInspectionAsync(
        Guid prevAppraisalId,
        string? pickedNumber,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var status = await mediator.Send(new GetConstructionChainStatusQuery(prevAppraisalId), cancellationToken);
        if (status?.BlockReason is not { } reason)
            return;

        var picked = string.IsNullOrWhiteSpace(pickedNumber) ? prevAppraisalId.ToString() : pickedNumber;

        var message = reason switch
        {
            ConstructionChainBlockReasons.ConstructionComplete =>
                $"Prior appraisal {picked} is already 100% complete " +
                $"({DescribeSource(status.Construction, picked)}).",
            ConstructionChainBlockReasons.LaterInspectionExists =>
                $"Prior appraisal {picked} already has a later inspection ({status.LaterInspection?.AppraisalNumber}).",
            ConstructionChainBlockReasons.OpenInspectionRequest =>
                $"Request {status.OpenInspectionRequest?.RequestNumber} is already inspecting this appraisal chain.",
            _ => $"Prior appraisal {picked} cannot be inspected again ({reason}).",
        };

        throw new BadRequestException(message, null, reason);
    }

    // "appraisal 69000012, inspection #3, 21/10/2026" — the appraisal that reports the 100%, which need not
    // be the one picked (then it is named; when it is the pick, repeating the number adds nothing).
    private static string DescribeSource(ConstructionChainProgress construction, string picked)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(construction.SourceAppraisalNumber)
            && !string.Equals(construction.SourceAppraisalNumber, picked, StringComparison.Ordinal))
            parts.Add($"appraisal {construction.SourceAppraisalNumber}");
        if (construction.SourceInspectionNumber is { } inspection)
            parts.Add($"inspection #{inspection}");
        if (construction.SourceCompletedAt is { } completedAt)
            parts.Add(completedAt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        return string.Join(", ", parts);
    }
}
