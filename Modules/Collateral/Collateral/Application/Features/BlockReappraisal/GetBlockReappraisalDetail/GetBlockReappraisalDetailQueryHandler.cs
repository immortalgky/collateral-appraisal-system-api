using System.Text.Json;

namespace Collateral.Application.Features.BlockReappraisal.GetBlockReappraisalDetail;

/// <summary>
/// Loads ProjectDetails.StructureJson for the given CollateralMaster and returns it
/// deserialized alongside the due-list header fields from BlockReappraisalDue.
/// Returns null when no ProjectDetail row exists → endpoint returns 404.
/// </summary>
public class GetBlockReappraisalDetailQueryHandler(
    ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetBlockReappraisalDetailQuery, BlockReappraisalDetailResult?>
{
    private static readonly JsonSerializerOptions DefaultOptions = new();

    public async Task<BlockReappraisalDetailResult?> Handle(
        GetBlockReappraisalDetailQuery query,
        CancellationToken cancellationToken)
    {
        // Single join: ProjectDetails (structure) + BlockReappraisalDue (header metadata)
        const string sql = """
            SELECT
                pd.CollateralMasterId,
                pd.StructureJson,
                brd.OldAppraisalNumber,
                brd.ProjectSellingPrice,
                brd.TotalUnits,
                brd.RemainingUnits,
                brd.LastAppraisedDate,
                brd.DueDate,
                pd.ProjectName,
                pd.ProjectType
            FROM collateral.ProjectDetails pd
            LEFT JOIN collateral.BlockReappraisalDue brd
                ON brd.CollateralMasterId = pd.CollateralMasterId
            WHERE pd.CollateralMasterId = @CollateralMasterId
            """;

        var row = await connectionFactory.QueryFirstOrDefaultAsync<ProjectDetailRow>(
            sql, new { query.CollateralMasterId });

        if (row is null)
            return null;

        // Deserialize the JSON snapshot using default options (PascalCase — matches the serializer
        // that produced it in the Appraisal module).
        BlockReappraisalStructureDto structure;
        try
        {
            structure = JsonSerializer.Deserialize<BlockReappraisalStructureDto>(
                row.StructureJson, DefaultOptions)
                ?? EmptyStructure(row.ProjectType, row.ProjectName);
        }
        catch (JsonException)
        {
            // Malformed snapshot — surface empty structure so the rest of the response is still useful.
            structure = EmptyStructure(row.ProjectType, row.ProjectName);
        }

        // A structurally-valid but partial payload (e.g. "{}") deserializes with null collections
        // (positional records have no defaults) — normalize to empty lists so callers never NRE.
        structure = structure with
        {
            Units = structure.Units ?? [],
            Models = structure.Models ?? [],
            Towers = structure.Towers ?? []
        };

        var soldUnits = structure.Units.Count(u => u.IsSold);

        return new BlockReappraisalDetailResult(
            CollateralMasterId: row.CollateralMasterId,
            OldAppraisalNumber: row.OldAppraisalNumber,
            ProjectName: row.ProjectName ?? structure.ProjectName,
            ProjectType: row.ProjectType ?? structure.ProjectType ?? string.Empty,
            ProjectSellingPrice: row.ProjectSellingPrice ?? structure.ProjectSellingPrice,
            TotalUnits: row.TotalUnits ?? structure.TotalUnits,
            RemainingUnits: row.RemainingUnits ?? structure.RemainingUnits,
            LastAppraisedDate: row.LastAppraisedDate,
            DueDate: row.DueDate,
            SoldUnits: soldUnits,
            Structure: structure);
    }

    private static BlockReappraisalStructureDto EmptyStructure(string? projectType, string? projectName) =>
        new(projectType, projectName, null, null, null, null, null, 0, 0, null, [], [], []);

    // Private Dapper projection — not exposed outside this handler
    private class ProjectDetailRow
    {
        public Guid CollateralMasterId { get; init; }
        public string StructureJson { get; init; } = "{}";
        public string? OldAppraisalNumber { get; init; }
        public decimal? ProjectSellingPrice { get; init; }
        public int? TotalUnits { get; init; }
        public int? RemainingUnits { get; init; }
        public DateTime? LastAppraisedDate { get; init; }
        public DateTime? DueDate { get; init; }
        public string? ProjectName { get; init; }
        public string? ProjectType { get; init; }
    }
}
