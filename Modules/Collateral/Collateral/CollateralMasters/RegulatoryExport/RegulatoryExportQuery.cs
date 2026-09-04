using System.Data;
using Collateral.Contracts.FileInterface;
using Dapper;
using Shared.Data;

namespace Collateral.CollateralMasters.RegulatoryExport;

public class RegulatoryExportQuery(ISqlConnectionFactory connectionFactory) : IRegulatoryExportQuery
{
    public async Task<IReadOnlyList<RegulatoryExportRow>> GetRowsAsync(CancellationToken cancellationToken = default)
    {
        // A procedure, not the view it replaced. Read with every column — which is exactly how this
        // method reads it — the view ran past the timeout below and produced no file at all, because
        // a CTE referenced more than once is re-expanded rather than reused. The procedure
        // materialises each shared step into a #temp, so it runs once and every step after it is
        // planned against a real row count. See the header of sp_RegulatoryExport.sql.
        //
        // ORDER BY and OPTION (MAXRECURSION 0) moved inside it. Neither could live in a view, so both
        // used to be appended here; the recursion cap now sits on the two steps that recurse instead
        // of on the whole statement.
        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<RawRow>(
            new CommandDefinition("collateral.sp_RegulatoryExport",
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken,
                // Kept from the view era. It should now be far out of reach — 2.4 seconds on the U3
                // set against 24.5 for the view — but a monthly job that produces nothing when it is
                // late is worse than one that takes a while.
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
