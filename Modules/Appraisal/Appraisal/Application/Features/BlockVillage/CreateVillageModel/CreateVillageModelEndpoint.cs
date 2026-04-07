namespace Appraisal.Application.Features.BlockVillage.CreateVillageModel;

public class CreateVillageModelEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/village-models",
                async (
                    Guid appraisalId,
                    CreateVillageModelRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateVillageModelCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result.Adapt<CreateVillageModelResponse>());
                }
            )
            .WithName("CreateVillageModel")
            .Produces<CreateVillageModelResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create village model")
            .WithDescription("Creates a new village house model for an appraisal.")
            .WithTags("Block Village");
    }
}
