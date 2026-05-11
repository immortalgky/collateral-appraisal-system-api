using Dapper;

namespace Appraisal.Application.Features.Invoices.GetEligibleAssignments;

public class GetEligibleAssignmentsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetEligibleAssignmentsQuery, IEnumerable<EligibleAssignmentDto>>
{
    public async Task<IEnumerable<EligibleAssignmentDto>> Handle(
        GetEligibleAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        var connection = sqlConnectionFactory.GetOpenConnection();
        const string sql =
            "SELECT * FROM appraisal.vw_EligibleAssignments WHERE AssigneeCompanyId = @CompanyId ORDER BY ReceivedDate DESC";

        var parameters = new DynamicParameters();
        parameters.Add("CompanyId", request.CompanyId.ToString());

        return await connection.QueryAsync<EligibleAssignmentDto>(sql, parameters);
    }
}
