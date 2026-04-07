namespace Appraisal.Application.Features.BlockVillage.GetVillageModels;

public class GetVillageModelsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/village-models",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(new GetVillageModelsQuery(appraisalId), cancellationToken);
                    return Results.Ok(result.Adapt<GetVillageModelsResponse>());
                }
            )
            .WithName("GetVillageModels")
            .Produces<GetVillageModelsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get village models")
            .WithDescription("Gets all village house models for an appraisal.")
            .WithTags("Block Village");
    }
}
