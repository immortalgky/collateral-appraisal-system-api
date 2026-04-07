namespace Appraisal.Application.Features.BlockCondo.UpdateCondoTower;

public class UpdateCondoTowerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/condo-towers/{towerId:guid}",
                async (
                    Guid appraisalId,
                    Guid towerId,
                    UpdateCondoTowerRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateCondoTowerCommand>()
                        with { AppraisalId = appraisalId, TowerId = towerId };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateCondoTower")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a condo tower")
            .WithDescription("Updates an existing condo tower for an appraisal.")
            .WithTags("Block Condo");
    }
}
