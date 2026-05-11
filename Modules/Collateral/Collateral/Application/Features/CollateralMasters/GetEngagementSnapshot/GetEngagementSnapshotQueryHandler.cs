using Dapper;

namespace Collateral.Application.Features.CollateralMasters.GetEngagementSnapshot;

/// <summary>
/// Returns the full engagement including the Snapshot JSON column.
/// Validates that the engagement belongs to the given master (parent-scoped 404).
/// </summary>
public class GetEngagementSnapshotQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetEngagementSnapshotQuery, GetEngagementSnapshotResult>
{
    public async Task<GetEngagementSnapshotResult> Handle(
        GetEngagementSnapshotQuery query,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT
                e.Id,
                e.CollateralMasterId,
                e.AppraisalId,
                e.AppraisalNumber,
                e.RequestId,
                e.RequestNumber,
                e.PropertyId,
                e.AppraisalType,
                e.AppraisalDate,
                e.AppraisedValue,
                e.AppraiserUserId,
                e.AppraisalCompanyId,
                e.AppraisalCompanyName,
                e.CreatedAt,
                e.Snapshot
            FROM collateral.CollateralEngagements e
            INNER JOIN collateral.CollateralMasters m ON m.Id = e.CollateralMasterId
            WHERE e.Id = @EngagementId
              AND e.CollateralMasterId = @MasterId
              AND m.IsDeleted = 0
            """;

        var p = new DynamicParameters();
        p.Add("EngagementId", query.EngagementId);
        p.Add("MasterId",     query.CollateralMasterId);

        var row = await connectionFactory.QueryFirstOrDefaultAsync<EngagementSnapshotRow>(sql, p);

        if (row is null)
            throw new NotFoundException("CollateralEngagement", query.EngagementId);

        return new GetEngagementSnapshotResult(
            row.Id,
            row.CollateralMasterId,
            row.AppraisalId,
            row.AppraisalNumber,
            row.RequestId,
            row.RequestNumber,
            row.PropertyId,
            row.AppraisalType,
            row.AppraisalDate,
            row.AppraisedValue,
            row.AppraiserUserId,
            row.AppraisalCompanyId,
            row.AppraisalCompanyName,
            row.CreatedAt,
            row.Snapshot ?? "{}");
    }

    private class EngagementSnapshotRow
    {
        public Guid Id { get; init; }
        public Guid CollateralMasterId { get; init; }
        public Guid AppraisalId { get; init; }
        public string AppraisalNumber { get; init; } = null!;
        public Guid RequestId { get; init; }
        public string RequestNumber { get; init; } = null!;
        public Guid PropertyId { get; init; }
        public string AppraisalType { get; init; } = null!;
        public DateTime AppraisalDate { get; init; }
        public decimal? AppraisedValue { get; init; }
        public string? AppraiserUserId { get; init; }
        public Guid? AppraisalCompanyId { get; init; }
        public string? AppraisalCompanyName { get; init; }
        public DateTime CreatedAt { get; init; }
        public string? Snapshot { get; init; }
    }
}
