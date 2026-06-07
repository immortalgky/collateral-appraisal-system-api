using Collateral.Contracts.BlockUnits;
using Dapper;
using MediatR;

namespace Collateral.CollateralMasters.Application.BlockUnits;

/// <summary>
/// Resolves the collateral master whose AppraisalSummary.LastAppraisalId matches the
/// supplied PrevAppraisalId and returns its full ProjectUnits list.
///
/// Collateral module owns ProjectUnits; Appraisal consumes this via Collateral.Contracts
/// (Appraisal already references Collateral.Contracts — no new dependency).
/// </summary>
public class GetProjectMasterUnitsByPrevAppraisalQueryHandler(
    ISqlConnectionFactory connectionFactory)
    : IRequestHandler<GetProjectMasterUnitsByPrevAppraisalQuery, ProjectMasterUnitsResult?>
{
    public async Task<ProjectMasterUnitsResult?> Handle(
        GetProjectMasterUnitsByPrevAppraisalQuery request,
        CancellationToken ct)
    {
        // Step 1: resolve the CollateralMaster via AppraisalSummary.LastAppraisalId on ProjectDetails
        const string headerSql = """
            SELECT pd.CollateralMasterId,
                   pd.ProjectType
            FROM   collateral.ProjectDetails pd
            INNER JOIN collateral.CollateralMasters cm ON cm.Id = pd.CollateralMasterId
            WHERE  pd.LastAppraisalId = @PrevAppraisalId
              AND  cm.CollateralType  = 'PRJ'
              AND  cm.IsMaster        = 1
              AND  cm.IsDeleted       = 0
              AND  pd.IsDeleted       = 0
            """;

        // Step 2: fetch all units for that master (both sold and unsold)
        const string unitsSql = """
            SELECT pu.SequenceNumber,
                   pu.IsSold,
                   pu.PurchaseBy,
                   pu.LoanBankName,
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
                   pu.LandArea
            FROM   collateral.ProjectUnits pu
            WHERE  pu.CollateralMasterId = @CollateralMasterId
            ORDER BY pu.SequenceNumber
            """;

        using var connection = connectionFactory.GetOpenConnection();

        var header = await connection.QueryFirstOrDefaultAsync<MasterHeaderRow>(
            headerSql, new { request.PrevAppraisalId });

        if (header is null)
            return null;

        var units = (await connection.QueryAsync<ProjectMasterUnitDto>(
            unitsSql, new { header.CollateralMasterId })).ToList().AsReadOnly();

        return new ProjectMasterUnitsResult(header.ProjectType, units);
    }

    private sealed record MasterHeaderRow(Guid CollateralMasterId, string ProjectType);
}
