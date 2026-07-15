using Collateral.Contracts.FileInterface;
using Dapper;
using Shared.Data;

namespace Collateral.CollateralMasters.RegulatoryExport;

public class RegulatoryExportQuery(ISqlConnectionFactory connectionFactory) : IRegulatoryExportQuery
{
    public async Task<IReadOnlyList<RegulatoryExportRow>> GetRowsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM collateral.vw_RegulatoryExport ORDER BY CollateralMasterId";

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
