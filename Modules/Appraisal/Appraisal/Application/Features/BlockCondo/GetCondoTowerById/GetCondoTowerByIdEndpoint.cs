namespace Appraisal.Application.Features.BlockCondo.GetCondoTowerById;

public class GetCondoTowerByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/condo-towers/{towerId:guid}",
                async (
                    Guid appraisalId,
                    Guid towerId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCondoTowerByIdQuery(appraisalId, towerId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetCondoTowerByIdResponse>();
                    return Results.Ok(response);
                }
            )
            .WithName("GetCondoTowerById")
            .Produces<GetCondoTowerByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get condo tower by ID")
            .WithDescription("Retrieves a specific condo tower by its ID.")
            .WithTags("Block Condo");
    }
}
