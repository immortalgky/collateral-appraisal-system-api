namespace Collateral.Application.Features.CollateralMasters.GetBackfillReport;

/// <summary>
/// Query parameters for the paginated backfill report endpoint.
/// </summary>
public record GetBackfillReportQuery(
    string? Status,
    PaginationRequest PaginationRequest) : IQuery<GetBackfillReportResult>;
