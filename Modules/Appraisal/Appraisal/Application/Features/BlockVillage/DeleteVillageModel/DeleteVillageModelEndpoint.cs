namespace Appraisal.Application.Features.BlockVillage.DeleteVillageModel;

public class DeleteVillageModelEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/appraisals/{appraisalId:guid}/village-models/{modelId:guid}",
                async (
                    Guid appraisalId,
                    Guid modelId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    await sender.Send(new DeleteVillageModelCommand(appraisalId, modelId), cancellationToken);
                    return Results.NoContent();
                }
            )
            .WithName("DeleteVillageModel")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete village model")
            .WithDescription("Deletes a village house model.")
            .WithTags("Block Village");
    }
}
