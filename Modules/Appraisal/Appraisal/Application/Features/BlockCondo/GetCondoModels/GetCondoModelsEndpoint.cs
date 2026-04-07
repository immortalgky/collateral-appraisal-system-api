namespace Appraisal.Application.Features.BlockCondo.GetCondoModels;

public class GetCondoModelsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/condo-models",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCondoModelsQuery(appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetCondoModelsResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetCondoModels")
            .Produces<GetCondoModelsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get condo models")
            .WithDescription("Retrieves all condo models for an appraisal.")
            .WithTags("Block Condo");
    }
}
