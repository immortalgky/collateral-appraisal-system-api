namespace Collateral.Application.Features.Reappraisal.InitiateReappraisal;

/// <summary>
/// Result of the InitiateReappraisal command — shown in the success popup.
/// </summary>
/// <param name="CreatedRequestIds">The prior AppraisalIds accepted (legacy AS400 books have none).</param>
/// <param name="AcceptedCount">Requests being created — one per accepted book, legacy ones included.</param>
/// <param name="Accepted">The accepted books, one request each.</param>
public record InitiateReappraisalResult(
    string GroupNumber,
    List<Guid> CreatedRequestIds,
    List<SkippedReappraisalItem> Skipped,
    int AcceptedCount = 0,
    List<AcceptedReappraisalItem>? Accepted = null
);

/// <summary>
/// A book a request is being created for. <see cref="PrevAppraisalId"/> is its CAS appraisal — the
/// request copies that appraisal's request (customers, titles, documents); NULL for a legacy AS400 book,
/// whose request starts empty.
/// </summary>
public record AcceptedReappraisalItem(string BookNumber, Guid? PrevAppraisalId);

/// <summary>
/// A book skipped during initiation. <see cref="Reason"/>:
///   <c>AlreadyInFlight</c> — a reappraisal of the book is already under way (an open reappraisal, or a
///   request for it still waiting to be submitted); a book selected twice in one batch gives one request and no
///   skip row, except that block books are reported per unit;
///   <c>AlreadyReviewed</c> — a nearby book whose reappraisal has already completed (it is superseded);
///   <c>NoBookNumber</c> — no appraisal number to key the book on, so no request can be tracked;
///   <c>NotDue</c> — the candidate is not on AS400's latest file (an old copy, or no longer due);
///   <c>PriorNotCompleted</c> — the book's appraisal in CAS is live but not completed (in progress...), so it
///   cannot be the prior of the request; its <see cref="AppraisalId"/> is that appraisal. A nearby pick of a deleted
///   or cancelled appraisal is skipped the same way. On the candidate path a deleted or cancelled appraisal counts as
///   not in CAS (legacy).
/// <see cref="AppraisalId"/> is the prior appraisal; NULL for a book that is not in CAS (legacy 99A…).
/// </summary>
public record SkippedReappraisalItem(
    Guid? AppraisalId,
    string? OldAppraisalReportNumber,
    string Reason
);
