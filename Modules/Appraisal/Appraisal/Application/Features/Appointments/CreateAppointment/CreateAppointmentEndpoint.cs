using Mapster;

namespace Appraisal.Application.Features.Appointments.CreateAppointment;

public class CreateAppointmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/appointments",
                async (
                    Guid appraisalId,
                    CreateAppointmentRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CreateAppointmentCommand(
                        appraisalId,
                        request.AppointmentDateTime,
                        request.AppointedBy,
                        request.LocationDetail,
                        request.ContactPerson,
                        request.ContactPhone);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateAppointmentResponse>();
                    return Results.Ok(response);
                }
            )
            .WithName("CreateAppointment")
            .Produces<CreateAppointmentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create appointment")
            .WithDescription("Create a new property survey appointment for an assignment.")
            .WithTags("Appointment");
    }
}
