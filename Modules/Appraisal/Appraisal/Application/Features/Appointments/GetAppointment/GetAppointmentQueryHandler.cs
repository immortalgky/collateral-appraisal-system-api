using Dapper;

namespace Appraisal.Application.Features.Appointments.GetAppointment;

public class GetAppointmentQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetAppointmentQuery, GetAppointmentResult>
{
    public async Task<GetAppointmentResult> Handle(
        GetAppointmentQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT TOP 1 * FROM appraisal.vw_AppointmentList
                           WHERE AppraisalId = @AppraisalId
                             AND Status IN ('Pending', 'Approved')
                           ORDER BY CreatedAt DESC
                           """;

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", query.AppraisalId);

        var appointment = await connectionFactory.QueryFirstOrDefaultAsync<AppointmentDto>(sql, parameters);

        return new GetAppointmentResult(appointment);
    }
}