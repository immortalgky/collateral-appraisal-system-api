using Request.Contracts.Requests.Dtos;

namespace Shared.Messaging.Events;

/// <summary>
/// Published once per candidate (or per nearby in-system appraisal) when the user submits
/// the InitiateReappraisal command. The Collateral module publishes this via its outbox;
/// the Request module's consumer creates and submits one reappraisal Request per message.
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
    /// <summary>The ReappraisalCandidate.Id that was MarkConsumed; NULL for InSystem path.</summary>
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

    // ── Requestor / creator ──────────────────────────────────────────────────
    public UserInfoDto Requestor { get; set; } = default!;
    public UserInfoDto Creator { get; set; } = default!;
}
