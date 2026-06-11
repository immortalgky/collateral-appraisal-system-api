using Collateral.Contracts;
using Dapper;

namespace Collateral.Application.Features.CollateralMasters.Lookup;

/// <summary>
/// Looks up a CollateralMaster by type-specific dedup key via Dapper.
/// Returns null when no matching master exists (caller maps to 404).
/// </summary>
public class LookupCollateralMasterQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<LookupCollateralMasterQuery, LookupCollateralMasterResult>
{
    public async Task<LookupCollateralMasterResult> Handle(
        LookupCollateralMasterQuery query,
        CancellationToken cancellationToken)
    {
        var type = query.Type;

        // For Land, the dedup search may land on an alias row — we get back the resolved IsMaster Id.
        // Widen each family arm to cover the new with-building variants (LB, LSB, LS) so callers
        // can lookup any code without 404ing on the variant. Family dispatch:
        //   L, LB                → Land family
        //   U                    → Condo
        //   LSL, LSB, LS         → Leasehold family
        //   MAC                  → Machine
        Guid? masterId = type switch
        {
            CollateralTypes.Land
                or CollateralTypes.LandWithBuilding       => await FindLandMasterIdAsync(query),
            CollateralTypes.Condo                          => await FindCondoMasterIdAsync(query),
            CollateralTypes.Leasehold
                or CollateralTypes.LeaseholdBuilding
                or CollateralTypes.LeaseholdWithBuilding   => await FindLeaseholdMasterIdAsync(query),
            CollateralTypes.Machine                        => await FindMachineMasterIdAsync(query),
            _                                              => null
        };

        if (masterId is null)
            throw new NotFoundException("CollateralMaster", $"type={query.Type}");

        // Load the master row from view (view already filters IsMaster = 1)
        var masterSql = "SELECT * FROM collateral.vw_CollateralMasters WHERE Id = @Id";
        var row = await connectionFactory.QueryFirstOrDefaultAsync<CollateralMasterViewRow>(
            masterSql, new { Id = masterId.Value });

        if (row is null)
            throw new NotFoundException("CollateralMaster", masterId.Value);

        // Load distinct prior company IDs for appeal exclusion
        var companySql = """
            SELECT DISTINCT AppraisalCompanyId
            FROM collateral.CollateralEngagements
            WHERE CollateralMasterId = @MasterId
              AND AppraisalCompanyId IS NOT NULL
            """;
        var companyIds = (await connectionFactory.QueryAsync<Guid?>(
            companySql, new { MasterId = masterId.Value }))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        // Most recent engagement — drives FE's most-recent-only appeal exclusion store
        // PR-4: AppraisedValue column dropped from CollateralEngagements; removed from this query.
        var lastEngagementSql = """
            SELECT TOP 1
                AppraisalId, AppraisalNumber, AppraisalType, AppraisalDate,
                AppraisalCompanyId, AppraisalCompanyName
            FROM collateral.CollateralEngagements
            WHERE CollateralMasterId = @MasterId
            ORDER BY AppraisalDate DESC
            """;
        var lastEngagement = await connectionFactory.QueryFirstOrDefaultAsync<LastEngagementSummaryDto>(
            lastEngagementSql, new { MasterId = masterId.Value });

        // For Land/LandWithBuilding: load alias titles so the caller sees the full title set for this property.
        // After the L→LB upgrade path, a Land master can be flipped to LandWithBuilding; aliases still apply.
        List<AliasTitleDto>? aliasTitles = null;
        if (type == CollateralTypes.Land || type == CollateralTypes.LandWithBuilding)
        {
            var aliasSql = """
                SELECT ld.TitleType, ld.TitleNumber, ld.SurveyNumber, ld.LandParcelNumber
                FROM collateral.CollateralMasters m
                INNER JOIN collateral.LandDetails ld ON ld.CollateralMasterId = m.Id
                WHERE m.IsDeleted = 0
                  AND m.IsMaster = 0
                  AND m.ParentMasterId = @MasterId
                """;
            var aliasRows = await connectionFactory.QueryAsync<AliasTitleRow>(
                aliasSql, new { MasterId = masterId.Value });
            aliasTitles = aliasRows
                .Select(r => new AliasTitleDto(r.TitleType, r.TitleNumber, r.SurveyNumber))
                .ToList();
        }

        return BuildResult(row, companyIds, aliasTitles, lastEngagement);
    }

    // ------------------------------------------------------------------
    // Per-type lookup helpers
    // ------------------------------------------------------------------

    private async Task<Guid?> FindLandMasterIdAsync(LookupCollateralMasterQuery q)
    {
        // NULL params act as wildcards so request-time partial lookup works.
        // Searches ALL rows (master + aliases) so any title in a multi-title group matches.
        // When the hit is an alias, navigates to its ParentMasterId and returns that as the result.
        var sql = """
            SELECT TOP 1
                CASE WHEN m.IsMaster = 1 THEN m.Id ELSE m.ParentMasterId END AS Id
            FROM collateral.CollateralMasters m
            INNER JOIN collateral.LandDetails ld ON ld.CollateralMasterId = m.Id
            WHERE m.IsDeleted = 0
              AND (@LandOfficeCode  IS NULL OR ld.LandOfficeCode  = @LandOfficeCode)
              AND (@Province        IS NULL OR ld.Province        = @Province)
              AND (@District        IS NULL OR ld.District        = @District)
              AND (@SubDistrict     IS NULL OR ld.SubDistrict     = @SubDistrict)
              AND (@TitleType       IS NULL OR ld.TitleType       = @TitleType)
              AND (@TitleNumber     IS NULL OR ld.TitleNumber     = @TitleNumber)
              AND (@SurveyNumber    IS NULL OR ld.SurveyNumber    = @SurveyNumber)
            """;

        var p = new DynamicParameters();
        p.Add("LandOfficeCode", q.LandOfficeCode);
        p.Add("Province",       q.Province);
        p.Add("District",       q.District);
        p.Add("SubDistrict",    q.SubDistrict);
        p.Add("TitleType",      q.TitleType);
        p.Add("TitleNumber",    q.TitleNumber);
        p.Add("SurveyNumber",   q.SurveyNumber);

        return await connectionFactory.QueryFirstOrDefaultAsync<Guid?>(sql, p);
    }

    private async Task<Guid?> FindCondoMasterIdAsync(LookupCollateralMasterQuery q)
    {
        // NULL params act as wildcards so request-time partial lookup works.
        // At appraisal completion the full key is enforced via the filtered unique index.
        var sql = """
            SELECT TOP 1 m.Id
            FROM collateral.CollateralMasters m
            INNER JOIN collateral.CondoDetails cd ON cd.CollateralMasterId = m.Id
            WHERE m.IsDeleted = 0
              AND (@LandOfficeCode          IS NULL OR cd.LandOfficeCode          = @LandOfficeCode)
              AND (@CondoRegistrationNumber IS NULL OR cd.CondoRegistrationNumber = @CondoRegistrationNumber)
              AND (@Building                IS NULL OR cd.BuildingNumber          = @Building)
              AND (@Floor                   IS NULL OR cd.FloorNumber             = @Floor)
              AND (@Unit                    IS NULL OR cd.RoomNumber              = @Unit)
              AND (@TitleNumber             IS NULL OR cd.TitleNumber             = @TitleNumber)
              AND (@TitleType               IS NULL OR cd.TitleType               = @TitleType)
            """;

        var p = new DynamicParameters();
        p.Add("LandOfficeCode",          q.LandOfficeCode);
        p.Add("CondoRegistrationNumber", q.CondoRegistrationNumber);
        p.Add("Building",                q.Building);
        p.Add("Floor",                   q.Floor);
        p.Add("Unit",                    q.Unit);
        p.Add("TitleNumber",             q.TitleNumber);
        p.Add("TitleType",               q.TitleType);

        return await connectionFactory.QueryFirstOrDefaultAsync<Guid?>(sql, p);
    }

    private async Task<Guid?> FindLeaseholdMasterIdAsync(LookupCollateralMasterQuery q)
    {
        var sql = """
            SELECT m.Id
            FROM collateral.CollateralMasters m
            INNER JOIN collateral.LeaseholdDetails lhd ON lhd.CollateralMasterId = m.Id
            WHERE m.IsDeleted = 0
              AND lhd.LeaseRegistrationNo = @ContractNo
              AND lhd.UnderlyingMasterId  = @UnderlyingMasterId
              AND lhd.Lessor              = @Lessor
              AND lhd.Lessee              = @Lessee
              AND lhd.LeaseTermStart      = @LeaseTermStart
            """;

        var p = new DynamicParameters();
        p.Add("ContractNo",         q.ContractNo);
        p.Add("UnderlyingMasterId", q.UnderlyingMasterId);
        p.Add("Lessor",             q.Lessor);
        p.Add("Lessee",             q.Lessee);
        // DateOnly → DateTime for Dapper
        p.Add("LeaseTermStart", q.LeaseTermStart.HasValue
            ? q.LeaseTermStart.Value.ToDateTime(TimeOnly.MinValue)
            : (DateTime?)null);

        return await connectionFactory.QueryFirstOrDefaultAsync<Guid?>(sql, p);
    }

    private async Task<Guid?> FindMachineMasterIdAsync(LookupCollateralMasterQuery q)
    {
        // Tier-1: by registration number when provided
        if (!string.IsNullOrWhiteSpace(q.MachineRegistrationNo))
        {
            var sql1 = """
                SELECT m.Id
                FROM collateral.CollateralMasters m
                INNER JOIN collateral.MachineDetails md ON md.CollateralMasterId = m.Id
                WHERE m.IsDeleted = 0
                  AND md.MachineRegistrationNo = @MachineRegistrationNo
                """;

            var p1 = new DynamicParameters();
            p1.Add("MachineRegistrationNo", q.MachineRegistrationNo);
            return await connectionFactory.QueryFirstOrDefaultAsync<Guid?>(sql1, p1);
        }

        // Tier-2: by composite key
        var sql2 = """
            SELECT m.Id
            FROM collateral.CollateralMasters m
            INNER JOIN collateral.MachineDetails md ON md.CollateralMasterId = m.Id
            WHERE m.IsDeleted = 0
              AND md.MachineRegistrationNo IS NULL
              AND md.SerialNo      = @SerialNo
              AND md.Brand         = @Brand
              AND md.Model         = @Model
              AND md.Manufacturer  = @Manufacturer
            """;

        var p2 = new DynamicParameters();
        p2.Add("SerialNo",     q.SerialNo);
        p2.Add("Brand",        q.Brand);
        p2.Add("Model",        q.Model);
        p2.Add("Manufacturer", q.Manufacturer);

        return await connectionFactory.QueryFirstOrDefaultAsync<Guid?>(sql2, p2);
    }

    // ------------------------------------------------------------------
    // Map view row → result
    // ------------------------------------------------------------------

    // Internal Dapper projection for alias title rows.
    // Uses regular setters (not init) to avoid Dapper 2.1 IL-emission edge cases
    // with sealed classes that use init-only properties.
    private class AliasTitleRow
    {
        public string TitleType { get; set; } = null!;
        public string TitleNumber { get; set; } = null!;
        public string? SurveyNumber { get; set; }
        public string? LandParcelNumber { get; set; }
    }

    private static LookupCollateralMasterResult BuildResult(
        CollateralMasterViewRow row,
        List<Guid> priorCompanyIds,
        List<AliasTitleDto>? aliasTitles,
        LastEngagementSummaryDto? lastEngagement)
    {
        LandDetailDto? landDetail = null;
        CondoDetailDto? condoDetail = null;
        LeaseholdDetailDto? leaseholdDetail = null;
        MachineDetailDto? machineDetail = null;

        // Family-grouped cases: a master's CollateralType may have flipped via LATEST-wins
        // (e.g. L → LB when a building was appraised). All variants share one detail DTO shape.
        switch (row.CollateralType)
        {
            case CollateralTypes.Land:
            case CollateralTypes.LandWithBuilding:
                landDetail = new LandDetailDto(
                    row.Land_LandOfficeCode!,
                    row.Land_Province!,
                    row.Land_District!,
                    row.Land_SubDistrict!,
                    row.Land_TitleType!,
                    row.Land_TitleNumber!,
                    row.Land_SurveyNumber,
                    row.Land_LandParcelNumber,
                    row.Land_Street,
                    row.Land_Village,
                    row.Land_Latitude,
                    row.Land_Longitude,
                    row.Land_LandShapeType,
                    row.Land_LandZoneType,
                    row.Land_UrbanPlanningType,
                    row.Land_AccessRoadWidth,
                    row.Land_RoadFrontage,
                    row.Land_LandArea,
                    row.IsUnderConstructionAtLastAppraisal ?? false,
                    row.OverallConstructionProgressPercent,
                    // PR-5: Land_LastConstructionInspectionId removed — CI list is in the engagement snapshot.
                    row.Land_LastAppraisalId,
                    row.Land_LastAppraisalNumber,
                    row.Land_LastAppraisedDate,
                    row.Land_UnitPrice,
                    row.Land_BuildingCost,
                    row.Land_AppraisalValue,
                    AliasTitles: aliasTitles ?? []);
                break;

            case CollateralTypes.Condo:
                condoDetail = new CondoDetailDto(
                    row.Condo_LandOfficeCode!,
                    row.Condo_CondoRegistrationNumber!,
                    row.Condo_BuildingNumber!,
                    row.Condo_FloorNumber!,
                    row.Condo_RoomNumber!,
                    row.Condo_TitleNumber!,
                    row.Condo_TitleType!,
                    row.Condo_CondoName,
                    row.Condo_Province,
                    row.Condo_UsableArea,
                    row.Condo_LocationType,
                    row.Condo_BuildingAge,
                    row.Condo_ConstructionYear,
                    row.Condo_ModelName,
                    row.Condo_LastAppraisalId,
                    row.Condo_LastAppraisalNumber,
                    row.Condo_LastAppraisedDate,
                    row.Condo_UnitPrice,
                    row.Condo_BuildingCost,
                    row.Condo_AppraisalValue);
                break;

            case CollateralTypes.Leasehold:
            case CollateralTypes.LeaseholdBuilding:
            case CollateralTypes.LeaseholdWithBuilding:
                leaseholdDetail = new LeaseholdDetailDto(
                    row.Lh_LeaseRegistrationNo!,
                    row.Lh_UnderlyingMasterId!.Value,
                    row.Lh_Lessor!,
                    row.Lh_Lessee!,
                    DateOnly.FromDateTime(row.Lh_LeaseTermStart!.Value),
                    row.Lh_LeaseTermEnd.HasValue ? DateOnly.FromDateTime(row.Lh_LeaseTermEnd.Value) : null,
                    row.Lh_LeaseTermMonths,
                    row.Lh_LastAppraisalId,
                    row.Lh_LastAppraisalNumber,
                    row.Lh_LastAppraisedDate);
                break;

            case CollateralTypes.Machine:
                machineDetail = new MachineDetailDto(
                    row.Machine_MachineRegistrationNo,
                    row.Machine_SerialNo,
                    row.Machine_Brand,
                    row.Machine_Model,
                    row.Machine_Manufacturer,
                    row.Machine_LastAppraisalId,
                    row.Machine_LastAppraisalNumber,
                    row.Machine_LastAppraisedDate);
                break;
        }

        return new LookupCollateralMasterResult(
            row.Id,
            row.CollateralType,
            row.OwnerName,
            row.CreatedAt,
            row.EngagementCount ?? 0,
            row.LastAppraisedDate,
            row.LastAppraisedValue,
            landDetail,
            condoDetail,
            leaseholdDetail,
            machineDetail,
            priorCompanyIds,
            lastEngagement);
    }
}
