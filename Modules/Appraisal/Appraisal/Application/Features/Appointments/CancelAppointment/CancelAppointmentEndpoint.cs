namespace Appraisal.Application.Features.Appointments.CancelAppointment;

public class CancelAppointmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/appraisals/{appraisalId:guid}/appointments/{appointmentId:guid}/cancel",
                async (
                    Guid appraisalId,
                    Guid appointmentId,
                    CancelAppointmentRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CancelAppointmentCommand(
                        appraisalId,
                        appointmentId,
                        request.ChangedBy,
                        request.Reason);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("CancelAppointment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Cancel appointment")
            .WithDescription("Cancel an appointment with an optional reason.")
            .WithTags("Appointment");
    }
}
