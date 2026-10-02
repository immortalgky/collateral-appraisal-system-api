using Request.Contracts.Requests.Dtos;

namespace Shared.Messaging.Events;

/// <summary>
/// Published once per candidate (or per nearby in-system appraisal) when the user submits
/// the InitiateReappraisal command. The Collateral module publishes this via its outbox;
/// the Request module's consumer creates one reappraisal Request per message and stops there —
/// staff review and submit it from the request list.
///
/// One event per candidate/appraisal so the consumer can be idempotent per unit of work.
/// All events for the same batch share <see cref="GroupNumber"/>.
///
/// Idempotency key: <see cref="GroupNumber"/> + (<see cref="PrevAppraisalId"/> ??
/// <see cref="SurveyNumber"/>) — unique within a batch.
/// </summary>
public record ReappraisalInitiatedIntegrationEvent : IntegrationEvent
{
    /// <summary>Shared group number for all requests in this initiation batch.</summary>
    public string GroupNumber { get; set; } = default!;

    /// <summary>
    /// Source path for this item:
    ///   <c>Candidate</c>  — came from a ReappraisalCandidate row (CandidateIds path).
    ///   <c>InSystem</c>   — came from a NearbyAppraisalId (in-system appraisal picked directly).
    /// </summary>
    public string Source { get; set; } = default!;

    // ── Candidate-path fields ────────────────────────────────────────────────
    /// <summary>The ReappraisalCandidate.Id this request is for; NULL for InSystem path.</summary>
    public Guid? CandidateId { get; set; }
    public string? SurveyNumber { get; set; }
    public string? CifNumber { get; set; }
    public string? CifName { get; set; }
    public string? CollateralId { get; set; }

    // ── Resolved appraisal link ──────────────────────────────────────────────
    /// <summary>
    /// The in-system Appraisal.Id this reappraisal is based on (resolved from SurveyNumber
    /// = AppraisalNumber). NULL when the SurveyNumber has no matching in-system appraisal.
    /// Becomes PrevAppraisalId on the new Request/Appraisal.
    /// </summary>
    public Guid? PrevAppraisalId { get; set; }

    /// <summary>
    /// The prior book's number when it is NOT an appraisal in this system — a legacy AS400 "99A…"
    /// book. Never set together with <see cref="PrevAppraisalId"/>. With it come the prior value and
    /// date from the bank's listing (appraisal.AS400ReportListing) — never the COLLATREV row's copies;
    /// null when the book has no listing row.
    /// </summary>
    public string? PrevAppraisalNumber { get; set; }
    public decimal? PrevAppraisalValue { get; set; }
    public DateTime? PrevAppraisalDate { get; set; }

    // ── Block-project unit ───────────────────────────────────────────────────
    /// <summary>
    /// The book is a block-project appraisal and this request reviews ONE unit of it (the collateral).
    /// <see cref="PrevAppraisalId"/> is the project appraisal; the request is filled from the project and
    /// <see cref="ProjectUnitId"/> instead of copying the project's request.
    /// </summary>
    public bool IsBlockUnit { get; set; }

    /// <summary>The unit matched for the collateral (appraisal.ProjectUnits.Id); NULL when none or several matched.</summary>
    public Guid? ProjectUnitId { get; set; }

    // ── Requestor / creator ──────────────────────────────────────────────────
    public UserInfoDto Requestor { get; set; } = default!;
    public UserInfoDto Creator { get; set; } = default!;
}
