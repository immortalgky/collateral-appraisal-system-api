namespace Appraisal.Application.Features.Appointments.GetAppointments;

public class GetAppointmentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/appointments",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetAppointmentsQuery(appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(new GetAppointmentsResponse(result.Appointments));
                }
            )
            .WithName("GetAppointments")
            .Produces<GetAppointmentsResponse>(StatusCodes.Status200OK)
            .WithSummary("Get appointments")
            .WithDescription("Get all appointments for an appraisal.")
            .WithTags("Appointment");
    }
}
