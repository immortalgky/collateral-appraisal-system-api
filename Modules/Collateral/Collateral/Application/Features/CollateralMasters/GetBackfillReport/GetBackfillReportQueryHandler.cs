using Dapper;
using Shared.Identity;

namespace Collateral.Application.Features.CollateralMasters.GetBackfillReport;

/// <summary>
/// Paginated backfill report query. Backed by a direct Dapper query on CollateralBackfillReports.
/// Admin-only.
/// Supports optional filtering by Status (Processed | SkippedMissingKey | Error).
/// </summary>
public class GetBackfillReportQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser
) : IQueryHandler<GetBackfillReportQuery, GetBackfillReportResult>
{
    public async Task<GetBackfillReportResult> Handle(
        GetBackfillReportQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("IntAdmin"))
            throw new UnauthorizedAccessException("Only Admin users can view the backfill report.");

        var sql = """
            SELECT Id, AppraisalId, Status, Message, RunAt
            FROM collateral.CollateralBackfillReports
            WHERE 1 = 1
            """;

        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            sql += " AND Status = @Status";
            p.Add("Status", query.Status.Trim());
        }

        var result = await connectionFactory.QueryPaginatedAsync<BackfillReportItemDto>(
            sql,
            "RunAt DESC",
            query.PaginationRequest,
            p);

        return new GetBackfillReportResult(result);
    }
}
