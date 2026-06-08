namespace Appraisal.Application.Features.Appointments.CancelReschedule;

public class CancelRescheduleAppointmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/appraisals/{appraisalId:guid}/appointments/{appointmentId:guid}/cancel-reschedule",
                async (
                    Guid appraisalId,
                    Guid appointmentId,
                    CancelRescheduleAppointmentRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CancelRescheduleAppointmentCommand(
                        appraisalId,
                        appointmentId,
                        request.ChangedBy,
                        request.Reason);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("CancelRescheduleAppointment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Cancel reschedule")
            .WithDescription("Discard a draft reschedule in Pending status and revert the appointment to the previous confirmed date.")
            .WithTags("Appointment");
    }
}
