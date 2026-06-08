namespace Request.Application.Features.Reappraisal.GetCandidateById;

/// <summary>
/// Full detail for one reappraisal candidate.
/// </summary>
public class ReappraisalCandidateDetail
{
    public Guid Id { get; set; }

    /// <summary>Matched in-system Appraisal.Id for this candidate's OldAppraisalReportNumber.
    /// NULL when the survey number doesn't resolve to any in-system appraisal (e.g. AS400-only).
    /// This is the real appraisal id (NOT the candidate Id) — used by map/pin detail navigation.</summary>
    public Guid? AppraisalId { get; set; }

    public string Status { get; set; } = default!;
    public string ReviewType { get; set; } = default!;
    public DateOnly AppraisalDate { get; set; }
    public int RemainingDay { get; set; }
    public string OldAppraisalReportNumber { get; set; } = default!;
    public string CifNumber { get; set; } = default!;
    public string? CustomerName { get; set; }
    public string CollateralId { get; set; } = default!;
    public string? CollateralName { get; set; }
    public string? CollateralAddress { get; set; }
    public string? CollateralCode { get; set; }
    public string? CollateralCategory { get; set; }
    public string? CollateralDescription { get; set; }
    public decimal? CurrentValue { get; set; }
    public DateOnly? ValuationDate { get; set; }
    public string? AoCode { get; set; }
    public string? AoName { get; set; }
    public string? TitleNumber { get; set; }
    public string? InternalExternal { get; set; }
    public string? BusinessSize { get; set; }
    public string? BusinessSizeDesc { get; set; }
    public decimal? MortgageAmount { get; set; }
    public int? PastDueDay { get; set; }
    public string? ApplicationNumber { get; set; }
    public string? FacilityCode { get; set; }
    public decimal? FacilityLimit { get; set; }
    public string? CarCode { get; set; }
    public string? SllOver100M { get; set; }
    public string? SllDescription { get; set; }

    // ── Trailing extension fields (input file pos 630–649) ───────────────────
    /// <summary>CIF stage indicator (pos 630).</summary>
    public string? Stage { get; set; }
    /// <summary>Banking segment — e.g. Retail / IBG (pos 631–640).</summary>
    public string? IBGRetail { get; set; }
    /// <summary>Review group code 1/2/3 (pos 641).</summary>
    public string? Group { get; set; }
    /// <summary>Effective appraisal date from AS400 extract (pos 642–649).</summary>
    public DateOnly? EffectiveDateAppraisal { get; set; }

    public string? FlagLessAge4Y { get; set; }
    public string? FlagGreaterAge4Y { get; set; }
    public string? CountAgeingDate { get; set; }
    public string? ExternalValuerName { get; set; }
    public string? InternalValuerName { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    /// <summary>Days between the matched in-system appraisal's AppointmentDateTime and today.
    /// NULL when SurveyNumber doesn't resolve to any in-system appraisal.</summary>
    public int? DaysSinceLastAppraisal { get; set; }

    /// <summary>True when a non-terminal reappraisal Appraisal points back at this candidate's prior
    /// appraisal (via PrevAppraisalId). Detected purely from the Appraisal table — no request dependency.</summary>
    public bool HasOpenAppraisal { get; set; }

    /// <summary>The open reappraisal Appraisal's Id.</summary>
    public Guid? OpenAppraisalId { get; set; }

    /// <summary>The open reappraisal Appraisal's AppraisalNumber — shown as "→ &lt;number&gt;" in the badge.</summary>
    public string? OpenAppraisalNumber { get; set; }

    public string? OpenAppraisalGroupTag { get; set; }

    /// <summary>
    /// Other nearby appraisals / Pending candidates within the radius (for the Group Appraisal
    /// selection table).  Excludes self, Consumed, Deleted, and already-in-flight rows.
    /// </summary>
    public List<NearbyReappraisalCandidate> NearbyGroupCandidates { get; set; } = [];
}

/// <summary>
/// Row in the nearby "Group Appraisal" table shown on the detail page.
/// Covers both in-system appraisals (Source="InSystem") and Pending COLLATREV
/// candidates (Source="Candidate").  When both exist for the same appraisal they
/// are merged into a single row that carries both AppraisalId and CandidateId.
/// </summary>
public class NearbyReappraisalCandidate
{
    /// <summary>In-system Appraisal.Id — null when the row comes from a candidate with no matching in-system appraisal.</summary>
    public Guid? AppraisalId { get; set; }

    /// <summary>ReappraisalCandidate.Id — null when the row is an in-system appraisal with no Pending candidate.</summary>
    public Guid? CandidateId { get; set; }

    /// <summary>"Candidate" when a Pending COLLATREV row exists; "InSystem" otherwise.</summary>
    public string Source { get; set; } = default!;

    public string OldAppraisalReportNumber { get; set; } = default!;
    public string? CustomerName { get; set; }
    public decimal? CurrentValue { get; set; }
    public DateOnly AppraisalDate { get; set; }
    public int RemainingDay { get; set; }

    /// <summary>Only present when Source="Candidate".</summary>
    public string? ReviewType { get; set; }

    /// <summary>Days between the last appraisal date (candidate ValuationDate or in-system
    /// AppointmentDateTime) and today. NULL when no date on either side.</summary>
    public int? DaysSinceLastAppraisal { get; set; }

    public double DistanceKm { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public record GetReappraisalCandidateByIdResult(ReappraisalCandidateDetail Candidate);
