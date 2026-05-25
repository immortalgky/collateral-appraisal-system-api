using Dapper;

namespace Appraisal.Application.Features.BlockUnitMaintenance.GetBlockUnitMaintenanceUnits;

public class GetBlockUnitMaintenanceUnitsQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetBlockUnitMaintenanceUnitsQuery, BlockUnitMaintenanceDetailDto?>
{
    public async Task<BlockUnitMaintenanceDetailDto?> Handle(
        GetBlockUnitMaintenanceUnitsQuery request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT p.Id              AS ProjectId,
                   a.AppraisalNumber AS AppraisalReportNo,
                   p.ProjectName     AS ProjectName,
                   p.ProjectType     AS ProjectType
            FROM   appraisal.Projects p
            LEFT JOIN appraisal.Appraisals a ON a.Id = p.AppraisalId
            WHERE  p.Id = @ProjectId;

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
            FROM   appraisal.ProjectUnits pu
            WHERE  pu.ProjectId = @ProjectId
            ORDER BY pu.SequenceNumber;
            """;

        var parameters = new DynamicParameters();
        parameters.Add("ProjectId", request.ProjectId);

        var connection = sqlConnectionFactory.GetOpenConnection();
        await using var multi = await connection.QueryMultipleAsync(sql, parameters);

        var headerRow = await multi.ReadSingleOrDefaultAsync<ProjectHeaderRow>();
        if (headerRow is null)
            return null;

        var units = (await multi.ReadAsync<BlockUnitMaintenanceUnitDto>()).ToList().AsReadOnly();

        // ProjectType is now stored as a text code ("U", "LB", "L") — project directly as string.
        var project = new BlockUnitMaintenanceProjectDto(
            headerRow.ProjectId,
            headerRow.AppraisalReportNo,
            headerRow.ProjectName,
            headerRow.ProjectType);

        return new BlockUnitMaintenanceDetailDto(project, units);
    }

    private sealed record ProjectHeaderRow(
        Guid ProjectId,
        string? AppraisalReportNo,
        string? ProjectName,
        string ProjectType);
}
