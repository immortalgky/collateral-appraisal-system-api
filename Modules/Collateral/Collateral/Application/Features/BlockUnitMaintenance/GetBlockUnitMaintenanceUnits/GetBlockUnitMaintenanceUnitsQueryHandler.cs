using Dapper;

namespace Collateral.Application.Features.BlockUnitMaintenance.GetBlockUnitMaintenanceUnits;

public class GetBlockUnitMaintenanceUnitsQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetBlockUnitMaintenanceUnitsQuery, BlockUnitMaintenanceDetailDto?>
{
    public async Task<BlockUnitMaintenanceDetailDto?> Handle(
        GetBlockUnitMaintenanceUnitsQuery request,
        CancellationToken cancellationToken)
    {
        // Two-result-set query:
        //   1. Header row from CollateralMasters + ProjectDetails (1 row or 0 = not found).
        //   2. Unit rows from ProjectUnits, ordered by SequenceNumber.
        const string sql = """
            SELECT cm.Id                    AS CollateralMasterId,
                   pd.LastAppraisalNumber   AS AppraisalReportNo,
                   pd.ProjectName           AS ProjectName,
                   pd.ProjectType           AS ProjectType
            FROM   collateral.CollateralMasters cm
            INNER JOIN collateral.ProjectDetails pd ON pd.CollateralMasterId = cm.Id
            WHERE  cm.Id = @CollateralMasterId
              AND  cm.CollateralType = 'PRJ'
              AND  cm.IsMaster = 1
              AND  cm.IsDeleted = 0
              AND  pd.IsDeleted = 0;

            SELECT pu.Id,
                   pu.SequenceNumber,
                   pu.ModelType,
                   pu.UsableArea,
                   pu.SellingPrice,
                   pu.Floor,
                   pu.TowerName,
                   pu.CondoRegistrationNumber,
                   pu.RoomNumber,
                   pu.PlotNumber,
                   pu.HouseNumber,
                   pu.NumberOfFloors,
                   pu.LandArea,
                   pu.IsSold,
                   pu.PurchaseBy,
                   pu.LoanBankName
            FROM   collateral.ProjectUnits pu
            WHERE  pu.CollateralMasterId = @CollateralMasterId
            ORDER BY pu.SequenceNumber;
            """;

        var parameters = new DynamicParameters();
        parameters.Add("CollateralMasterId", request.CollateralMasterId);

        var connection = sqlConnectionFactory.GetOpenConnection();
        await using var multi = await connection.QueryMultipleAsync(sql, parameters);

        var headerRow = await multi.ReadSingleOrDefaultAsync<ProjectHeaderRow>();
        if (headerRow is null)
            return null;

        var units = (await multi.ReadAsync<BlockUnitMaintenanceUnitDto>()).ToList().AsReadOnly();

        var project = new BlockUnitMaintenanceProjectDto(
            headerRow.CollateralMasterId,
            headerRow.AppraisalReportNo,
            headerRow.ProjectName,
            headerRow.ProjectType);

        return new BlockUnitMaintenanceDetailDto(project, units);
    }

    private sealed record ProjectHeaderRow(
        Guid CollateralMasterId,
        string? AppraisalReportNo,
        string? ProjectName,
        string ProjectType);
}
