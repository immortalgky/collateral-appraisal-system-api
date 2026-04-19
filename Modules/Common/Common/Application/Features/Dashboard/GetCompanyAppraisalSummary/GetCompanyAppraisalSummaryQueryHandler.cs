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

        var companyId = currentUserService.CompanyId;

        // When date params are provided, query source tables directly because the
        // denormalized summary table (CompanyAppraisalSummaries) has no per-appraisal
        // date column — only a rolling LastUpdatedAt.
        if (query.From.HasValue || query.To.HasValue)
        {
            var dateConditions = new List<string> { "a.IsDeleted = 0" };

            if (query.From.HasValue)
            {
                dateConditions.Add("a.CreatedAt >= @From");
                parameters.Add("From", query.From.Value.ToDateTime(TimeOnly.MinValue));
            }

            if (query.To.HasValue)
            {
                dateConditions.Add("a.CreatedAt < DATEADD(day, 1, @To)");
                parameters.Add("To", query.To.Value.ToDateTime(TimeOnly.MinValue));
            }

            if (companyId.HasValue)
            {
                dateConditions.Add("aa.AssigneeCompanyId = CAST(@CompanyId AS nvarchar(450))");
                parameters.Add("CompanyId", companyId.Value);
            }

            var whereClause = string.Join(" AND ", dateConditions);

            var sourceSql = $"""
                SELECT
                    TRY_CAST(aa.AssigneeCompanyId AS uniqueidentifier) AS CompanyId,
                    comp.Name                                           AS CompanyName,
                    COUNT(*)                                            AS AssignedCount,
                    SUM(CASE WHEN a.Status = 'Completed' THEN 1 ELSE 0 END) AS CompletedCount
                FROM appraisal.Appraisals a
                INNER JOIN (
                    SELECT AppraisalId, AssigneeCompanyId,
                           ROW_NUMBER() OVER (PARTITION BY AppraisalId ORDER BY CreatedAt DESC) AS rn
                    FROM appraisal.AppraisalAssignments
                    WHERE AssigneeCompanyId IS NOT NULL
                      AND AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                ) aa ON aa.AppraisalId = a.Id AND aa.rn = 1
                LEFT JOIN auth.Companies comp
                    ON comp.Id = TRY_CAST(aa.AssigneeCompanyId AS uniqueidentifier)
                WHERE {whereClause}
                GROUP BY aa.AssigneeCompanyId, comp.Name
                ORDER BY AssignedCount DESC
                """;

            var sourceItems = await connection.QueryAsync<CompanyAppraisalSummaryDto>(sourceSql, parameters);
            return new GetCompanyAppraisalSummaryResult(sourceItems.ToList());
        }

        // Fast path: no date filter — use the pre-aggregated summary table.
        var companyFilter = "";
        if (companyId.HasValue)
        {
            companyFilter = "WHERE CompanyId = @CompanyId";
            parameters.Add("CompanyId", companyId.Value);
        }

        var sql = $"""
            SELECT CompanyId, CompanyName, AssignedCount, CompletedCount
            FROM common.CompanyAppraisalSummaries
            {companyFilter}
            ORDER BY AssignedCount DESC
            """;

        var items = await connection.QueryAsync<CompanyAppraisalSummaryDto>(sql, parameters);

        return new GetCompanyAppraisalSummaryResult(items.ToList());
    }
}
