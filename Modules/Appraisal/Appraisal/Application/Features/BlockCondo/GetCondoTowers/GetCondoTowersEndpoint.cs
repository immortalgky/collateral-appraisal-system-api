namespace Appraisal.Application.Features.BlockCondo.GetCondoTowers;

public class GetCondoTowersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/condo-towers",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCondoTowersQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetCondoTowersResponse>();
                    return Results.Ok(response);
                }
            )
            .WithName("GetCondoTowers")
            .Produces<GetCondoTowersResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get condo towers")
            .WithDescription("Retrieves all condo towers for an appraisal.")
            .WithTags("Block Condo");
    }
}
