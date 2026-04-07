namespace Appraisal.Application.Features.BlockCondo.UpdateCondoModel;

public class UpdateCondoModelEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/condo-models/{modelId:guid}",
                async (
                    Guid appraisalId,
                    Guid modelId,
                    UpdateCondoModelRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateCondoModelCommand>()
                        with
                        {
                            AppraisalId = appraisalId,
                            ModelId = modelId
                        };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateCondoModel")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a condo model")
            .WithDescription("Updates an existing condo model for an appraisal.")
            .WithTags("Block Condo");
    }
}
