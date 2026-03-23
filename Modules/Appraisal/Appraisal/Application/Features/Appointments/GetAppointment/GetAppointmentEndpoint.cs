namespace Appraisal.Application.Features.Appointments.GetAppointment;

public class GetAppointmentEndpoint : ICarterModule
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
                    var query = new GetAppointmentQuery(appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    if (result.Appointment is null)
                        return Results.NotFound();

                    return Results.Ok(new GetAppointmentResponse(result.Appointment));
                }
            )
            .WithName("GetAppointment")
            .Produces<GetAppointmentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get active appointment")
            .WithDescription("Get the active appointment (Pending or Approved) for an appraisal.")
            .WithTags("Appointment");
    }
}
