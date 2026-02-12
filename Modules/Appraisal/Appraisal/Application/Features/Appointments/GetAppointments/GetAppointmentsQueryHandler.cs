using Dapper;

namespace Appraisal.Application.Features.Appointments.GetAppointments;

public class GetAppointmentsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetAppointmentsQuery, GetAppointmentsResult>
{
    public async Task<GetAppointmentsResult> Handle(
        GetAppointmentsQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT * FROM appraisal.vw_AppointmentList
            WHERE AppraisalId = @AppraisalId
            ORDER BY CreatedOn DESC
            """;

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", query.AppraisalId);

        var appointments = await connectionFactory.QueryAsync<AppointmentDto>(sql, parameters);

        return new GetAppointmentsResult(appointments.ToList());
    }
}
