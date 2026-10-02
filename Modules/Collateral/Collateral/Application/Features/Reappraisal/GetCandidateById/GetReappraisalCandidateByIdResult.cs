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
    public DateOnly? AppraisalDate { get; set; }
    /// <summary>Review due date AS400 sent (ReviewDate); RemainingDay counts down to it.</summary>
    public DateOnly? ReviewDate { get; set; }
    public int? RemainingDay { get; set; }
    public string OldAppraisalReportNumber { get; set; } = default!;

    /// <summary><see cref="OldAppraisalReportNumber"/> as CAS stores it (AS400's 'B' prefix dropped).</summary>
    public string NormalizedSurveyNumber { get; set; } = default!;

    /// <summary>Where the prior book lives: <c>CAS</c>, <c>AS400Legacy</c> (a 99A… book) or <c>Unknown</c>.</summary>
    public string PriorAppraisalSource { get; set; } = default!;
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

    /// <summary>The reappraisal Request created by Initiate and not yet submitted (Draft / New).</summary>
    public Guid? OpenRequestId { get; set; }
    public string? OpenRequestNumber { get; set; }

    /// <summary>The row reviews one unit of a block project (ReappraisalCandidate.IsBlockUnit).</summary>
    public bool IsBlockUnit { get; set; }

    /// <summary>The project and the unit matched for a block-project row; NULL otherwise.</summary>
    public BlockUnitInfo? Unit { get; set; }

    // ── Processed books: the reappraisal the book produced (same rule as the processed tab) ──
    public Guid? NewAppraisalId { get; set; }
    public string? NewAppraisalNumber { get; set; }
    public string? NewAppraisalStatus { get; set; }
    public string? NewAppraisalGroupTag { get; set; }
    public DateTime? NewAppraisalSubmittedAt { get; set; }
    public DateTime? NewAppraisalCompletedAt { get; set; }

    /// <summary>First and latest AS400 files that listed this book.</summary>
    public DateOnly FirstSeenFileDate { get; set; }
    public DateOnly LastSeenFileDate { get; set; }

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
    public DateOnly? AppraisalDate { get; set; }
    /// <summary>Review due date AS400 sent; NULL for an in-system appraisal not on the file.</summary>
    public DateOnly? ReviewDate { get; set; }
    public int? RemainingDay { get; set; }
    public string? ReviewType { get; set; }
    public int? DaysSinceLastAppraisal { get; set; }
    public double DistanceKm { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    /// <summary>The book is under review already (open reappraisal or waiting request) — shown, not selectable.</summary>
    public bool IsInProgress { get; set; }
}

/// <summary>
/// A block-project unit row: its project (from the appraisal the book names) and, when exactly one unit
/// matched (collateral.vw_ReappraisalCandidateUnits), that unit with its appraised price.
/// </summary>
public class BlockUnitInfo
{
    public Guid ProjectAppraisalId { get; set; }
    public string ProjectAppraisalNumber { get; set; } = default!;
    public string? ProjectType { get; set; }
    public string? ProjectName { get; set; }
    public DateTime? ProjectValuationDate { get; set; }

    /// <summary>Units matched: 1 = found, 0 = none, more = ambiguous.</summary>
    public int MatchedUnits { get; set; }

    /// <summary><c>Ticket</c>, <c>CollateralName</c> or <c>CollateralAddress</c>; NULL when none matched.</summary>
    public string? MatchedBy { get; set; }
    public string? TowerName { get; set; }
    public int? Floor { get; set; }
    public string? RoomNumber { get; set; }
    public string? CondoRegistrationNumber { get; set; }
    public decimal? UsableArea { get; set; }
    public string? ModelType { get; set; }
    public string? HouseNumber { get; set; }
    public string? PlotNumber { get; set; }
    public decimal? LandArea { get; set; }
    public decimal? UnitPrice { get; set; }
}

public record GetReappraisalCandidateByIdResult(ReappraisalCandidateDetail Candidate);
