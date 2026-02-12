using Dapper;

namespace Appraisal.Application.Features.Assignments.GetAssignments;

public class GetAssignmentsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetAssignmentsQuery, GetAssignmentsResult>
{
    public async Task<GetAssignmentsResult> Handle(
        GetAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT * FROM appraisal.vw_AssignmentList
            WHERE AppraisalId = @AppraisalId
            ORDER BY CreatedOn DESC
            """;

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", query.AppraisalId);

        var assignments = await connectionFactory.QueryAsync<AssignmentDto>(sql, parameters);

        return new GetAssignmentsResult(assignments.ToList());
    }
}
