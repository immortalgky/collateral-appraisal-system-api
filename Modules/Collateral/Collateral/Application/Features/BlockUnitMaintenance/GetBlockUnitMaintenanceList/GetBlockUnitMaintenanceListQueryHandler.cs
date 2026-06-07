using Dapper;

namespace Collateral.Application.Features.BlockUnitMaintenance.GetBlockUnitMaintenanceList;

public class GetBlockUnitMaintenanceListQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetBlockUnitMaintenanceListQuery, BlockUnitMaintenanceListResult>
{
    public async Task<BlockUnitMaintenanceListResult> Handle(
        GetBlockUnitMaintenanceListQuery request,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            conditions.Add(
                "(AppraisalReportNo LIKE @Search OR ProjectName LIKE @Search OR CustomerName LIKE @Search)");
            parameters.Add("Search", $"%{request.Search}%");
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectType))
        {
            conditions.Add("ProjectType = @ProjectType");
            parameters.Add("ProjectType", request.ProjectType);
        }

        if (!string.IsNullOrWhiteSpace(request.Developer))
        {
            conditions.Add("Developer LIKE @Developer");
            parameters.Add("Developer", $"%{request.Developer}%");
        }

        var where = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : "";

        // Whitelist sortable columns to defeat SQL injection.
        var sortColumn = request.SortBy?.ToLowerInvariant() switch
        {
            "appraisalreportno" => "AppraisalReportNo",
            "projectname"       => "ProjectName",
            "customername"      => "CustomerName",
            "projecttype"       => "ProjectType",
            "developer"         => "Developer",
            "totalunits"        => "TotalUnits",
            "soldunits"         => "SoldUnits",
            "unsoldunits"       => "UnsoldUnits",
            "updatedon"         => "UpdatedOn",
            "updatedby"         => "UpdatedBy",
            _                   => null
        };

        var sortDir = string.Equals(request.SortDir, "asc", StringComparison.OrdinalIgnoreCase)
            ? "ASC"
            : "DESC";

        var orderBy = sortColumn is not null
            ? $"{sortColumn} {sortDir}, UpdatedOn DESC"
            : "UpdatedOn DESC";

        const string view = "collateral.vw_BlockMaintenanceList";
        const string listColumns = """
            CollateralMasterId, AppraisalReportNo, CustomerName,
            ProjectName, ProjectType, Developer,
            TotalUnits, SoldUnits, UnsoldUnits, UpdatedOn, UpdatedBy
            """;

        var offset = request.PageNumber * request.PageSize;
        var dataSql =
            $"SELECT {listColumns} FROM {view} {where} " +
            $"ORDER BY {orderBy} OFFSET {offset} ROWS FETCH NEXT {request.PageSize} ROWS ONLY";
        var countSql = $"SELECT COUNT(CollateralMasterId) FROM {view} {where}";

        var connection = sqlConnectionFactory.GetOpenConnection();

        using var multi = await connection.QueryMultipleAsync(
            countSql + "; " + dataSql,
            parameters);

        var totalCount = await multi.ReadFirstOrDefaultAsync<int>();
        var items = (await multi.ReadAsync<BlockUnitMaintenanceListDto>()).ToList();

        return new BlockUnitMaintenanceListResult(
            items,
            totalCount,
            request.PageNumber,
            request.PageSize);
    }
}
