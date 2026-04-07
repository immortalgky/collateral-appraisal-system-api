namespace Appraisal.Application.Features.BlockCondo.CreateCondoModel;

public class CreateCondoModelEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/condo-models",
                async (
                    Guid appraisalId,
                    CreateCondoModelRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateCondoModelCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateCondoModelResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/condo-models/{response.Id}",
                        response);
                }
            )
            .WithName("CreateCondoModel")
            .Produces<CreateCondoModelResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a condo model")
            .WithDescription("Creates a new condo model for an appraisal.")
            .WithTags("Block Condo");
    }
}
