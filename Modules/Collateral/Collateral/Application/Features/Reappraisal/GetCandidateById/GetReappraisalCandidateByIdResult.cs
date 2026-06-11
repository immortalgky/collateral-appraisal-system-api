namespace Collateral.Application.Features.Reappraisal.GetCandidateById;

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
    public string? Stage { get; set; }
    public string? IBGRetail { get; set; }
    public string? Group { get; set; }
    public DateOnly? EffectiveDateAppraisal { get; set; }

    public string? FlagLessAge4Y { get; set; }
    public string? FlagGreaterAge4Y { get; set; }
    public string? CountAgeingDate { get; set; }
    public string? ExternalValuerName { get; set; }
    public string? InternalValuerName { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public int? DaysSinceLastAppraisal { get; set; }
    public bool HasOpenAppraisal { get; set; }
    public Guid? OpenAppraisalId { get; set; }
    public string? OpenAppraisalNumber { get; set; }
    public string? OpenAppraisalGroupTag { get; set; }

    public List<NearbyReappraisalCandidate> NearbyGroupCandidates { get; set; } = [];
}

/// <summary>
/// Row in the nearby "Group Appraisal" table shown on the detail page.
/// </summary>
public class NearbyReappraisalCandidate
{
    public Guid? AppraisalId { get; set; }
    public Guid? CandidateId { get; set; }
    public string Source { get; set; } = default!;
    public string OldAppraisalReportNumber { get; set; } = default!;
    public string? CustomerName { get; set; }
    public decimal? CurrentValue { get; set; }
    public DateOnly AppraisalDate { get; set; }
    public int RemainingDay { get; set; }
    public string? ReviewType { get; set; }
    public int? DaysSinceLastAppraisal { get; set; }
    public double DistanceKm { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public record GetReappraisalCandidateByIdResult(ReappraisalCandidateDetail Candidate);
