using Collateral.Contracts;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Appraisal.Application.Features.Appraisals.GetInProgressAppraisalsByCollateral;

/// <summary>
/// Reads the appraisal-side tables (the appraiser may edit keys after submit), not the Collateral
/// master, so a title is found while its appraisal is still open.
///
/// Key semantics mirror GET /collateral-masters/lookup and the master upsert dedup key: exact
/// equality on the full key, no trimming, database collation for case. A partial key matches
/// nothing (empty list, as the lookup returns nothing). Address comes from the entered address,
/// not the DOPA one, as GetAppraisalForCollateral feeds the master.
///
/// Each arm only matches the property types that feed its master (land L/LB, condo U, machine MAC);
/// a lease agreement property (LSL/LS/LSB/LSU) carries a land or condo detail too but keys a leasehold master.
/// Block appraisals own no AppraisalProperties rows, so they never match.
/// </summary>
public class GetInProgressAppraisalsByCollateralQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetInProgressAppraisalsByCollateralQuery, IReadOnlyList<InProgressAppraisalDto>>
{
    // "In progress" = Status NOT IN ('Completed','Cancelled','Rejected') AND not deleted.
    // 'Rejected' is not an AppraisalStatus value today; kept on request so it stays excluded if added.
    // The predicate is repeated in each statement on purpose: static SQL, nothing concatenated.
    private const string LandSql = """
        SELECT DISTINCT a.Id AS AppraisalId, a.AppraisalNumber, a.Status
        FROM appraisal.Appraisals a
        JOIN appraisal.AppraisalProperties p ON p.AppraisalId = a.Id
        JOIN appraisal.LandAppraisalDetails ld ON ld.AppraisalPropertyId = p.Id
        JOIN appraisal.LandTitles t ON t.LandAppraisalDetailId = ld.Id
        WHERE a.IsDeleted = 0
          AND a.Status NOT IN ('Completed', 'Cancelled', 'Rejected')
          AND p.PropertyType IN ('L', 'LB')
          AND ld.Province    = @Province
          AND ld.District    = @District
          AND ld.SubDistrict = @SubDistrict
          AND t.TitleNumber  = @TitleNumber
        ORDER BY AppraisalNumber, AppraisalId
        """;

    private const string CondoSql = """
        SELECT DISTINCT a.Id AS AppraisalId, a.AppraisalNumber, a.Status
        FROM appraisal.Appraisals a
        JOIN appraisal.AppraisalProperties p ON p.AppraisalId = a.Id
        JOIN appraisal.CondoAppraisalDetails cd ON cd.AppraisalPropertyId = p.Id
        WHERE a.IsDeleted = 0
          AND a.Status NOT IN ('Completed', 'Cancelled', 'Rejected')
          AND p.PropertyType = 'U'
          AND cd.CondoRegistrationNumber = @CondoRegistrationNumber
          AND cd.BuildingNumber          = @Building
          AND cd.FloorNumber             = @Floor
          AND cd.RoomNumber              = @Unit
          AND cd.Province                = @Province
          AND cd.District                = @District
          AND cd.SubDistrict             = @SubDistrict
        ORDER BY AppraisalNumber, AppraisalId
        """;

    private const string MachineSql = """
        SELECT DISTINCT a.Id AS AppraisalId, a.AppraisalNumber, a.Status
        FROM appraisal.Appraisals a
        JOIN appraisal.AppraisalProperties p ON p.AppraisalId = a.Id
        JOIN appraisal.MachineryAppraisalDetails md ON md.AppraisalPropertyId = p.Id
        WHERE a.IsDeleted = 0
          AND a.Status NOT IN ('Completed', 'Cancelled', 'Rejected')
          AND p.PropertyType = 'MAC'
          AND md.RegistrationNumber = @MachineRegistrationNo
        ORDER BY AppraisalNumber, AppraisalId
        """;

    public async Task<IReadOnlyList<InProgressAppraisalDto>> Handle(
        GetInProgressAppraisalsByCollateralQuery q, CancellationToken cancellationToken)
    {
        var (sql, args, complete) = q.Type switch
        {
            CollateralTypes.Land or CollateralTypes.LandWithBuilding => (
                LandSql,
                (object)new { q.Province, q.District, q.SubDistrict, q.TitleNumber },
                !AnyBlank(q.Province, q.District, q.SubDistrict, q.TitleNumber)),
            CollateralTypes.Condo => (
                CondoSql,
                new { q.CondoRegistrationNumber, q.Building, q.Floor, q.Unit, q.Province, q.District, q.SubDistrict },
                !AnyBlank(q.CondoRegistrationNumber, q.Building, q.Floor, q.Unit, q.Province, q.District, q.SubDistrict)),
            CollateralTypes.Machine => (
                MachineSql,
                new { q.MachineRegistrationNo },
                !AnyBlank(q.MachineRegistrationNo)),
            _ => (string.Empty, new { }, false)
        };

        if (!complete) return [];

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<InProgressAppraisalDto>(
            new CommandDefinition(sql, args, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private static bool AnyBlank(params string?[] values) => values.Any(string.IsNullOrWhiteSpace);
}
