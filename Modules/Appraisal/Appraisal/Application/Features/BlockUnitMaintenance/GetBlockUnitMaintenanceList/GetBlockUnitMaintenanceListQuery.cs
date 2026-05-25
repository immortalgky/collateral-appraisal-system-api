namespace Appraisal.Application.Features.BlockUnitMaintenance.GetBlockUnitMaintenanceList;

public record GetBlockUnitMaintenanceListQuery(
    int PageNumber,
    int PageSize,
    /// <summary>
    /// Full-text search across AppraisalReportNo, ProjectName, and CustomerName.
    /// </summary>
    string? Search,
    /// <summary>
    /// Optional project-type filter. "Condo" or "LandAndBuilding". Null = all types.
    /// </summary>
    string? ProjectType,
    /// <summary>
    /// Optional developer-name contains filter.
    /// </summary>
    string? Developer,
    string? SortBy,
    string? SortDir
) : IQuery<BlockUnitMaintenanceListResult>;

public record BlockUnitMaintenanceListResult(
    IReadOnlyList<BlockUnitMaintenanceListDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);
