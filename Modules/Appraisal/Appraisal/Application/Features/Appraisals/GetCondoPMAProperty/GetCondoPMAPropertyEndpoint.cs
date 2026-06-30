public class GetCondoPMAPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/condo-pma",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCondoPMAPropertyQuery(appraisalId, propertyId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetCondoPMAPropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetCondoPMAProperty")
            .Produces<GetCondoPMAPropertyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get condo pma property detail")
            .WithDescription("Retrieves a condo pma property with its detail by property ID.")
            .WithTags("Appraisal Properties");
    }
}