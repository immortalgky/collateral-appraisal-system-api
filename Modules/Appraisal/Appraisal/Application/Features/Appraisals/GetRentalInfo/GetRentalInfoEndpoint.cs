namespace Appraisal.Application.Features.Appraisals.GetRentalInfo;

public class GetRentalInfoEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/rental-info",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetRentalInfoQuery(appraisalId, propertyId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetRentalInfoResponse>();
                    return Results.Ok(response);
                }
            )
            .WithName("GetRentalInfo")
            .Produces<GetRentalInfoResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get rental info")
            .WithDescription("Retrieves the rental info for a lease agreement property.")
            .WithTags("Appraisal Properties");
    }
}
