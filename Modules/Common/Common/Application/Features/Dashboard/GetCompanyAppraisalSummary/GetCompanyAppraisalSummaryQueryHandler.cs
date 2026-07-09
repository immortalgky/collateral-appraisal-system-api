using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;

namespace Common.Application.Features.Dashboard.GetCompanyAppraisalSummary;

public class GetCompanyAppraisalSummaryQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService
) : IQueryHandler<GetCompanyAppraisalSummaryQuery, GetCompanyAppraisalSummaryResult>
{
    public async Task<GetCompanyAppraisalSummaryResult> Handle(
        GetCompanyAppraisalSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();
        var parameters = new DynamicParameters();
        var conditions = new List<string>();

        // Filter on the appraisal's CreatedAt (the canonical "Assigned" date axis).
        if (query.From.HasValue)
        {
            conditions.Add("CreatedAt >= @From");
            parameters.Add("From", query.From.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (query.To.HasValue)
        {
            // Exclusive upper bound + 1 day so the whole To-day is included
            // (CreatedAt is a datetime; <= midnight would drop same-day rows).
            conditions.Add("CreatedAt < DATEADD(day, 1, @To)");
            parameters.Add("To", query.To.Value.ToDateTime(TimeOnly.MinValue));
        }

        var companyId = currentUserService.CompanyId;
        if (companyId.HasValue)
        {
            conditions.Add("CompanyId = @CompanyId");
            parameters.Add("CompanyId", companyId.Value);
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        // All four counts come from one live source at the appraisal grain, so
        // CompletedCount + InProgressCount + OverdueCount == AssignedCount for every company.
        var sql = $"""
            SELECT CompanyId,
                   COALESCE(MAX(NULLIF(CompanyName, N'')), N'(pending)') AS CompanyName,
                   COUNT(*)          AS AssignedCount,
                   SUM(IsCompleted)  AS CompletedCount,
                   SUM(IsOverdue)    AS OverdueCount,
                   SUM(IsInProgress) AS InProgressCount
            FROM common.vw_CompanyAppraisalSummaryLive
            {where}
            GROUP BY CompanyId
            ORDER BY AssignedCount DESC
            """;

        var items = await connection.QueryAsync<CompanyAppraisalSummaryDto>(sql, parameters);

        return new GetCompanyAppraisalSummaryResult(items.ToList());
    }
}
