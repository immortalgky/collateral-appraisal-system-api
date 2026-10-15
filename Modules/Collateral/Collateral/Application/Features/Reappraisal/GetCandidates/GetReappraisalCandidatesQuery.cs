using Shared.Pagination;

namespace Collateral.Application.Features.Reappraisal.GetCandidates;

/// <summary>
/// Returns a paginated list of reappraisal candidates with optional filters.
/// Maps to GET /reappraisal/candidates.
/// </summary>
/// <param name="Status">
/// <c>Pending</c> (default — the to-do list) or <c>Deleted</c> ("not reviewing this round"), both only
/// books on AS400's latest file; or <c>Consumed</c> — processed books, the whole history.
/// </param>
/// <param name="Search">One box for book number (as sent or as CAS stores it), CIF or customer name.</param>
/// <param name="PriorSource"><c>CAS</c>, <c>AS400Legacy</c>, <c>Unknown</c>, or <c>NonCAS</c> (either of the last two).</param>
/// <param name="NewAppraisalState">Consumed tab only: <c>Appraising</c>, <c>Completed</c>, <c>Cancelled</c> or <c>NotFound</c>.</param>
/// <param name="InProgress">True: only books with an open reappraisal or a request waiting; false: only the others.</param>
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
    string? SortDir = null,
    string? Status = null,
    string? Search = null,
    string? PriorSource = null,
    bool? InProgress = null,
    string? NewAppraisalState = null
) : IQuery<GetReappraisalCandidatesResult>;
