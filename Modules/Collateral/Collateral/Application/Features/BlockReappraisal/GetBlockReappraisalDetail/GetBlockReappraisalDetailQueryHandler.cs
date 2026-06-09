using Dapper;

namespace Collateral.Application.Features.BlockReappraisal.GetBlockReappraisalDetail;

/// <summary>
/// Loads ProjectDetails + ProjectUnits for the given CollateralMaster and returns the
/// due-list header fields from BlockReappraisalDue alongside the unit list.
///
/// Phase 1 removed StructureJson from ProjectDetails; this handler reads the first-class
/// collateral.ProjectUnits rows instead and builds the same response shape so the frontend
/// needs no change.
///
/// Returns null when no ProjectDetail row exists → endpoint returns 404.
/// </summary>
public class GetBlockReappraisalDetailQueryHandler(
    ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetBlockReappraisalDetailQuery, BlockReappraisalDetailResult?>
{
    public async Task<BlockReappraisalDetailResult?> Handle(
        GetBlockReappraisalDetailQuery query,
        CancellationToken cancellationToken)
    {
        // Query 1: project header + due-list metadata
        const string headerSql = """
            SELECT
                pd.CollateralMasterId,
                pd.ProjectName,
                pd.ProjectType,
                pd.Developer,
                pd.Address,
                pd.Province,
                pd.Latitude,
                pd.Longitude,
                pd.TotalUnits,
                pd.RemainingUnits,
                pd.ProjectSellingPrice,
                brd.OldAppraisalNumber,
                brd.LastAppraisedDate,
                brd.DueDate,
                brd.ProjectSellingPrice AS BrdProjectSellingPrice,
                brd.TotalUnits        AS BrdTotalUnits,
                brd.RemainingUnits    AS BrdRemainingUnits
            FROM collateral.ProjectDetails pd
            LEFT JOIN collateral.BlockReappraisalDue brd
                ON brd.CollateralMasterId = pd.CollateralMasterId
            WHERE pd.CollateralMasterId = @CollateralMasterId
                AND pd.IsDeleted = 0
            """;

        // Query 2: per-unit rows
        const string unitsSql = """
            SELECT
                pu.SequenceNumber,
                pu.IsSold,
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
            FROM collateral.ProjectUnits pu
            WHERE pu.CollateralMasterId = @CollateralMasterId
            ORDER BY pu.SequenceNumber
            """;

        using var connection = connectionFactory.GetOpenConnection();

        var headerRow = await connection.QueryFirstOrDefaultAsync<ProjectDetailHeaderRow>(
            headerSql, new { query.CollateralMasterId });

        if (headerRow is null)
            return null;

        var unitRows = (await connection.QueryAsync<BlockReappraisalUnitDto>(
            unitsSql, new { query.CollateralMasterId })).AsList();

        var soldUnits = unitRows.Count(u => u.IsSold);

        // Build Models and Towers from distinct non-null values in the unit list —
        // mirrors what the old StructureJson snapshot stored separately.
        var models = unitRows
            .Where(u => u.ModelType is not null)
            .Select(u => u.ModelType!)
            .Distinct()
            .Select(m => new BlockReappraisalModelDto(m))
            .ToList();

        var towers = unitRows
            .Where(u => u.TowerName is not null)
            .Select(u => u.TowerName!)
            .Distinct()
            .Select(t => new BlockReappraisalTowerDto(t))
            .ToList();

        var structure = new BlockReappraisalStructureDto(
            ProjectType: headerRow.ProjectType,
            ProjectName: headerRow.ProjectName,
            Developer: headerRow.Developer,
            Address: headerRow.Address,
            Province: headerRow.Province,
            Latitude: headerRow.Latitude,
            Longitude: headerRow.Longitude,
            TotalUnits: headerRow.BrdTotalUnits ?? headerRow.TotalUnits,
            RemainingUnits: headerRow.BrdRemainingUnits ?? headerRow.RemainingUnits,
            ProjectSellingPrice: headerRow.BrdProjectSellingPrice ?? headerRow.ProjectSellingPrice,
            Units: unitRows,
            Models: models,
            Towers: towers);

        return new BlockReappraisalDetailResult(
            CollateralMasterId: headerRow.CollateralMasterId,
            OldAppraisalNumber: headerRow.OldAppraisalNumber,
            ProjectName: headerRow.ProjectName,
            ProjectType: headerRow.ProjectType ?? string.Empty,
            ProjectSellingPrice: headerRow.BrdProjectSellingPrice ?? headerRow.ProjectSellingPrice,
            TotalUnits: headerRow.BrdTotalUnits ?? headerRow.TotalUnits,
            RemainingUnits: headerRow.BrdRemainingUnits ?? headerRow.RemainingUnits,
            LastAppraisedDate: headerRow.LastAppraisedDate,
            DueDate: headerRow.DueDate,
            SoldUnits: soldUnits,
            Structure: structure);
    }

    // Private Dapper projection for the header row
    private class ProjectDetailHeaderRow
    {
        public Guid CollateralMasterId { get; init; }
        public string? ProjectName { get; init; }
        public string? ProjectType { get; init; }
        public string? Developer { get; init; }
        public string? Address { get; init; }
        public string? Province { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public int TotalUnits { get; init; }
        public int RemainingUnits { get; init; }
        public decimal? ProjectSellingPrice { get; init; }
        public string? OldAppraisalNumber { get; init; }
        public DateTime? LastAppraisedDate { get; init; }
        public DateTime? DueDate { get; init; }
        // From BlockReappraisalDue — preferred when available (snapshot at due-date creation)
        public decimal? BrdProjectSellingPrice { get; init; }
        public int? BrdTotalUnits { get; init; }
        public int? BrdRemainingUnits { get; init; }
    }
}
