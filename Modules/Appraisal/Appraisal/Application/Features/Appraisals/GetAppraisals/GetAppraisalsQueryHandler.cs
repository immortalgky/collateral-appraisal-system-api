using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

/// <summary>
/// Handler for getting all Appraisals with pagination and filtering.
/// Uses SQL view + Dapper for efficient read queries.
/// </summary>
public class GetAppraisalsQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetAppraisalsQuery, GetAppraisalsResult>
{
    public async Task<GetAppraisalsResult> Handle(
        GetAppraisalsQuery query,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT * FROM appraisal.vw_AppraisalList";
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        var filter = query.Filter;
        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                conditions.Add("Status = @Status");
                parameters.Add("Status", filter.Status);
            }

            if (!string.IsNullOrWhiteSpace(filter.Priority))
            {
                conditions.Add("Priority = @Priority");
                parameters.Add("Priority", filter.Priority);
            }

            if (!string.IsNullOrWhiteSpace(filter.AppraisalType))
            {
                conditions.Add("AppraisalType = @AppraisalType");
                parameters.Add("AppraisalType", filter.AppraisalType);
            }

            if (filter.AssigneeUserId.HasValue)
            {
                conditions.Add("AssigneeUserId = @AssigneeUserId");
                parameters.Add("AssigneeUserId", filter.AssigneeUserId.Value);
            }
        }

        if (conditions.Count > 0) sql += " WHERE " + string.Join(" AND ", conditions);

        var result = await connectionFactory.QueryPaginatedAsync<AppraisalDto>(
            sql,
            "CreatedOn DESC",
            query.PaginationRequest,
            parameters);

        return new GetAppraisalsResult(result);
    }
}