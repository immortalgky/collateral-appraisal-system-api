using Collateral.Contracts.FileInterface;
using Dapper;
using Shared.Data;

namespace Collateral.CollateralMasters.RegulatoryExport;

public class RegulatoryExportV2Query(ISqlConnectionFactory connectionFactory) : IRegulatoryExportV2Query
{
    public async Task<IReadOnlyList<RegulatoryExportRow>> GetRowsAsync(CancellationToken cancellationToken = default)
    {
        // OPTION (MAXRECURSION 0) MUST live here — SQL Server does not allow it inside a view, and the
        // view walks PrevAppraisalId to find each chain's root. Without it the default cap of 100
        // levels aborts the whole query with Msg 530 and no file is produced at all. The view carries
        // a Path-based cycle guard, so lifting the cap is safe.
        //
        // LatestAppraisalNumber alone is the sort key: unlike v1 there is no master dimension, and the
        // view emits at most one row per appraisal number, so this is already deterministic.
        const string sql = """
            SELECT * FROM collateral.vw_RegulatoryExportV2
            ORDER BY LatestAppraisalNumber
            OPTION (MAXRECURSION 0)
            """;

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<RawRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken,
                // The recursive walk over every completed appraisal takes longer than the 30s default.
                commandTimeout: 600));
        return rows.Select(Map).ToList();
    }

    private static RegulatoryExportRow Map(RawRow r) => new(
        LatestAppraisalNumber: r.LatestAppraisalNumber,
        CollateralType: r.CollateralType,
        HostCollateralId: r.HostCollateralId,
        LatestAppraisalType: r.LatestAppraisalType,
        IsUnderConstruction: r.IsUnderConstruction,
        ConstructionProgressPercent: r.ConstructionProgressPercent,
        LatestAppraisalValue: r.LatestAppraisalValue,
        EarliestAppraisalValue: r.EarliestAppraisalValue,
        CurrentValue: r.CurrentValue,
        SellingPrice: r.SellingPrice,
        NumberOfFloors: r.NumberOfFloors,
        BuildingAge: r.BuildingAge,
        LatestAppraisalDate: r.LatestAppraisalDate,
        LatestProgressiveAppraisalDate: r.LatestProgressiveAppraisalDate,
        EarliestAppraisalDate: r.EarliestAppraisalDate,
        LatestAppraisalCompanyId: r.LatestAppraisalCompanyId,
        DopaCode: r.DopaCode,
        LandAreaSqWa: r.LandAreaSqWa,
        BuildingArea: r.BuildingArea,
        BuildingTypeCode: r.BuildingTypeCode,
        BuildingTypeDescription: r.BuildingTypeDescription
    );

    private sealed class RawRow
    {
        public string CollateralType { get; init; } = null!;
        public string? HostCollateralId { get; init; }
        public string? LatestAppraisalNumber { get; init; }
        public string? LatestAppraisalType { get; init; }
        public bool IsUnderConstruction { get; init; }
        public decimal? ConstructionProgressPercent { get; init; }
        public decimal? LatestAppraisalValue { get; init; }
        public decimal? EarliestAppraisalValue { get; init; }
        public decimal? CurrentValue { get; init; }
        public decimal? SellingPrice { get; init; }
        public int? NumberOfFloors { get; init; }
        public int? BuildingAge { get; init; }
        public DateTime? LatestAppraisalDate { get; init; }
        public DateTime? LatestProgressiveAppraisalDate { get; init; }
        public DateTime? EarliestAppraisalDate { get; init; }
        public Guid? LatestAppraisalCompanyId { get; init; }
        public string? DopaCode { get; init; }
        public decimal? LandAreaSqWa { get; init; }
        public decimal? BuildingArea { get; init; }
        public string? BuildingTypeCode { get; init; }
        public string? BuildingTypeDescription { get; init; }
    }
}
