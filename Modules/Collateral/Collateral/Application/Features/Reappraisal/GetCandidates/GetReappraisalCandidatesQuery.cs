using Shared.Pagination;

namespace Collateral.Application.Features.Reappraisal.GetCandidates;

/// <summary>
/// Returns a paginated list of Pending reappraisal candidates with optional filters.
/// Maps to GET /reappraisal/candidates.
/// </summary>
public record GetReappraisalCandidatesQuery(
    PaginationRequest Pagination,
    string? CustomerName = null,
    string? OldAppraisalReportNumber = null,
    string? CifNumber = null,
    string? CollateralId = null,
    string? ReviewType = null,
    DateOnly? ReviewDateFrom = null,
    DateOnly? ReviewDateTo = null,
    int? RemainingDayFrom = null,
    int? RemainingDayTo = null,
    string? SortBy = null,
    string? SortDir = null
) : IQuery<GetReappraisalCandidatesResult>;
