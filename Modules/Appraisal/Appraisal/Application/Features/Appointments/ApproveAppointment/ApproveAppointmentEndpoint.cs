namespace Appraisal.Application.Features.Appointments.ApproveAppointment;

public class ApproveAppointmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch(
                "/appraisals/{appraisalId:guid}/appointments/{appointmentId:guid}/approve",
                async (
                    Guid appraisalId,
                    Guid appointmentId,
                    ApproveAppointmentRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new ApproveAppointmentCommand(
                        appraisalId,
                        appointmentId,
                        request.ApprovedBy);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("ApproveAppointment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Approve appointment")
            .WithDescription("Approve a pending appointment.")
            .WithTags("Appointment");
    }
}
