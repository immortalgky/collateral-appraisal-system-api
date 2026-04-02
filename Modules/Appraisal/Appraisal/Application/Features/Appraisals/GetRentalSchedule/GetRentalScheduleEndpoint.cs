namespace Appraisal.Application.Features.Appraisals.GetRentalSchedule;

public class GetRentalScheduleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/rental-schedule",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetRentalScheduleQuery(appraisalId, propertyId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetRentalScheduleResponse>();
                    return Results.Ok(response);
                }
            )
            .WithName("GetRentalSchedule")
            .Produces<GetRentalScheduleResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get rental schedule")
            .WithDescription("Retrieves the computed rental schedule for a lease agreement property.")
            .WithTags("Appraisal Properties");
    }
}
