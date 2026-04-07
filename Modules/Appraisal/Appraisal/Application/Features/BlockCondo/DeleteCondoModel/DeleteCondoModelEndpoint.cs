namespace Appraisal.Application.Features.BlockCondo.DeleteCondoModel;

public class DeleteCondoModelEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/appraisals/{appraisalId:guid}/condo-models/{modelId:guid}",
                async (
                    Guid appraisalId,
                    Guid modelId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new DeleteCondoModelCommand(appraisalId, modelId);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("DeleteCondoModel")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete a condo model")
            .WithDescription("Deletes a condo model from an appraisal.")
            .WithTags("Block Condo");
    }
}
