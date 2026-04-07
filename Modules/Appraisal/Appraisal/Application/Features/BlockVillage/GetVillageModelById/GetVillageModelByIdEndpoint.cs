namespace Appraisal.Application.Features.BlockVillage.GetVillageModelById;

public class GetVillageModelByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/village-models/{modelId:guid}",
                async (
                    Guid appraisalId,
                    Guid modelId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(new GetVillageModelByIdQuery(appraisalId, modelId), cancellationToken);
                    return Results.Ok(result.Adapt<GetVillageModelByIdResponse>());
                }
            )
            .WithName("GetVillageModelById")
            .Produces<GetVillageModelByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get village model by ID")
            .WithDescription("Gets a specific village house model by ID.")
            .WithTags("Block Village");
    }
}
