using Dapper;

namespace Appraisal.Application.Features.Assignments.GetAssignments;

public class GetAssignmentsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetAssignmentsQuery, GetAssignmentsResult>
{
    public async Task<GetAssignmentsResult> Handle(
        GetAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        var sql = """
                  SELECT * 
                  FROM appraisal.vw_Assignment
                  WHERE AppraisalId = @AppraisalId
                  ORDER BY CreatedAt DESC
                  """;

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", query.AppraisalId);

        var assignments = await connectionFactory.QueryAsync<AssignmentDto>(sql, parameters);

        return new GetAssignmentsResult(assignments.ToList());
    }
}