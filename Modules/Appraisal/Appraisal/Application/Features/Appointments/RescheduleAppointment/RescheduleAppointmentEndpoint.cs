namespace Appraisal.Application.Features.Appointments.RescheduleAppointment;

public class RescheduleAppointmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/appraisals/{appraisalId:guid}/appointments/{appointmentId:guid}/reschedule",
                async (
                    Guid appraisalId,
                    Guid appointmentId,
                    RescheduleAppointmentRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RescheduleAppointmentCommand(
                        appraisalId,
                        appointmentId,
                        request.ChangedBy,
                        request.NewDateTime,
                        request.Reason);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("RescheduleAppointment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Reschedule appointment")
            .WithDescription("Reschedule an appointment to a new date/time.")
            .WithTags("Appointment");
    }
}
