namespace Collateral.Application.Features.BlockReappraisal.GetBlockReappraisalDueList;

/// <summary>
/// Returns a paginated list of block-project CollateralMasters on the reappraisal due list.
/// Backed by collateral.vw_BlockReappraisalDueList; <paramref name="Status"/> picks the tab:
/// Pending (default) or Deleted (marked not required).
/// </summary>
public record GetBlockReappraisalDueListQuery(
    string? Status,
    string? Search,
    DateTime? LastAppraisedDateFrom,
    DateTime? LastAppraisedDateTo,
    int? RemainingDayMin,
    int? RemainingDayMax,
    string? SortBy,
    string? SortDir,
    PaginationRequest PaginationRequest) : IQuery<GetBlockReappraisalDueListResult>;

public record GetBlockReappraisalDueListResult(PaginatedResult<BlockReappraisalDueListItem> Items);

public record BlockReappraisalDueListItem(
    Guid CollateralMasterId,
    string? OldAppraisalNumber,
    string? ProjectName,
    string ProjectType,
    decimal? ProjectSellingPrice,
    int TotalUnits,
    int RemainingUnits,
    DateTime? LastAppraisedDate,
    DateTime DueDate,
    string Status,
    int RemainingDay,
    string? NotRequiredRemark);
