using Collateral.Contracts.FileInterface;
using Dapper;
using Shared.Data;

namespace Collateral.CollateralMasters.RegulatoryExport;

public class RegulatoryExportQuery(ISqlConnectionFactory connectionFactory) : IRegulatoryExportQuery
{
    public async Task<IReadOnlyList<RegulatoryExportRow>> GetRowsAsync(CancellationToken cancellationToken = default)
    {
        // Ordering by CollateralMasterId alone is no longer sufficient: the view is one record per
        // (chain, master), and a master can belong to several chains, so CollateralMasterId repeats.
        // Without a unique tie-breaker the row order in the file would vary between runs.
        //
        // OPTION (MAXRECURSION 0) MUST live here — SQL Server does not allow it inside a view.
        // Without it the default limit of 100 levels applies, and a construction-inspection chain
        // longer than 100 aborts the whole query with Msg 530, producing no regulatory file at all.
        // The view already carries a Path-based cycle guard, so lifting the cap is safe.
        const string sql = """
            SELECT * FROM collateral.vw_RegulatoryExport
            ORDER BY CollateralMasterId, LatestAppraisalNumber
            OPTION (MAXRECURSION 0)
            """;

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<RawRow>(sql);
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
        public Guid CollateralMasterId { get; init; }
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
