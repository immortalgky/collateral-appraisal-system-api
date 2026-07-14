namespace Collateral.Application.Features.BlockReappraisal.GetBlockReappraisalDueList;

/// <summary>
/// Returns a paginated list of block-project CollateralMasters that are pending reappraisal.
/// Backed by collateral.vw_BlockReappraisalDueList (Status = 'Pending').
/// </summary>
public record GetBlockReappraisalDueListQuery(
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
    int RemainingDay);
