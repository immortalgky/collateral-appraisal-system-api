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

        // External users see only their company
        var companyFilter = "";
        var companyId = currentUserService.CompanyId;
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
