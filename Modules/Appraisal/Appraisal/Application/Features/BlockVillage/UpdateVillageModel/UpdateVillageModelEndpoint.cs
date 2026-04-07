namespace Appraisal.Application.Features.BlockVillage.UpdateVillageModel;

public class UpdateVillageModelEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/village-models/{modelId:guid}",
                async (
                    Guid appraisalId,
                    Guid modelId,
                    UpdateVillageModelRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateVillageModelCommand>()
                        with { AppraisalId = appraisalId, ModelId = modelId };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateVillageModel")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update village model")
            .WithDescription("Updates a village house model.")
            .WithTags("Block Village");
    }
}
