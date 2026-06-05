namespace Appraisal.Application.Features.Appointments.GetAppointmentHistory;

public class GetAppointmentHistoryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/appointment-history",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetAppointmentHistoryQuery(appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(new GetAppointmentHistoryResponse(result.Events));
                }
            )
            .WithName("GetAppointmentHistory")
            .Produces<GetAppointmentHistoryResponse>(StatusCodes.Status200OK)
            .WithSummary("Get appointment history")
            .WithDescription("Get the timeline of appointment and fee events for an appraisal.")
            .WithTags("Appointment")
            .RequireAuthorization();
    }
}
