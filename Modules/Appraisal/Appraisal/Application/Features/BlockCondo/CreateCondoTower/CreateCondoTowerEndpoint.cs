namespace Appraisal.Application.Features.BlockCondo.CreateCondoTower;

public class CreateCondoTowerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/condo-towers",
                async (
                    Guid appraisalId,
                    CreateCondoTowerRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateCondoTowerCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateCondoTowerResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/condo-towers/{response.Id}",
                        response);
                }
            )
            .WithName("CreateCondoTower")
            .Produces<CreateCondoTowerResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a condo tower")
            .WithDescription("Creates a new condo tower for an appraisal.")
            .WithTags("Block Condo");
    }
}
