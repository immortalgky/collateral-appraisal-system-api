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

        if (query.From.HasValue)
        {
            conditions.Add("Date >= @From");
            parameters.Add("From", query.From.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (query.To.HasValue)
        {
            conditions.Add("Date <= @To");
            parameters.Add("To", query.To.Value.ToDateTime(TimeOnly.MinValue));
        }

        var companyId = currentUserService.CompanyId;
        if (companyId.HasValue)
        {
            conditions.Add("CompanyId = @CompanyId");
            parameters.Add("CompanyId", companyId.Value);
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $"""
            SELECT CompanyId,
                   COALESCE(MAX(CASE WHEN CompanyName <> N'(pending)' THEN CompanyName END), N'(pending)') AS CompanyName,
                   SUM(AssignedCount)  AS AssignedCount,
                   SUM(CompletedCount) AS CompletedCount
            FROM common.CompanyAppraisalSummaries
            {where}
            GROUP BY CompanyId
            ORDER BY AssignedCount DESC
            """;

        var items = await connection.QueryAsync<CompanyAppraisalSummaryDto>(sql, parameters);

        return new GetCompanyAppraisalSummaryResult(items.ToList());
    }
}
