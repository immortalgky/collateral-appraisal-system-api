namespace Appraisal.Application.Features.Appraisals.GetLandProperty;

public class GetLandPMAPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/land-building-pma",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetLandPMAPropertyQuery(appraisalId, propertyId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetLandPMAPropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetLandPMAProperty")
            .Produces<GetLandPMAPropertyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get land pma property detail")
            .WithDescription("Retrieves a land pma property with its detail by property ID.")
            .WithTags("Appraisal Properties");
    }
}