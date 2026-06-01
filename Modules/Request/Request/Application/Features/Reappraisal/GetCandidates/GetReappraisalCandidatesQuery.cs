using Shared.Pagination;

namespace Request.Application.Features.Reappraisal.GetCandidates;

/// <summary>
/// Returns a paginated list of Pending reappraisal candidates with optional filters.
/// Maps to GET /api/v1/reappraisal/candidates.
/// </summary>
public record GetReappraisalCandidatesQuery(
    PaginationRequest Pagination,

    /// <summary>Full or partial CIF name / customer name (LIKE %value%).</summary>
    string? CustomerName = null,

    /// <summary>Exact or partial Old Appraisal Report Number (= SurveyNumber) match (LIKE %value%).</summary>
    string? OldAppraisalReportNumber = null,

    /// <summary>CIF number exact match.</summary>
    string? CifNumber = null,

    /// <summary>Collateral ID exact match.</summary>
    string? CollateralId = null,

    /// <summary>ReviewType code: 1 = Normal, 2 = Before Stage 3, 3 = Stage 3.</summary>
    string? ReviewType = null,

    /// <summary>ReviewDate (AS400-provided due date) start of range (inclusive).</summary>
    DateOnly? ReviewDateFrom = null,

    /// <summary>ReviewDate (AS400-provided due date) end of range (inclusive).</summary>
    DateOnly? ReviewDateTo = null,

    /// <summary>RemainingDay lower bound (inclusive, can be negative = already overdue).</summary>
    int? RemainingDayFrom = null,

    /// <summary>RemainingDay upper bound (inclusive).</summary>
    int? RemainingDayTo = null

    // TODO(confirm): TotalOutstanding filter omitted — field not present in Collateral Review Interface.
    // Candidates: MortgageAmount, FacilityLimit, or CurrentValue as substitute.
) : IQuery<GetReappraisalCandidatesResult>;
