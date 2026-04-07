namespace Appraisal.Application.Features.BlockCondo.DeleteCondoTower;

public class DeleteCondoTowerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/appraisals/{appraisalId:guid}/condo-towers/{towerId:guid}",
                async (
                    Guid appraisalId,
                    Guid towerId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new DeleteCondoTowerCommand(appraisalId, towerId);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                }
            )
            .WithName("DeleteCondoTower")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete a condo tower")
            .WithDescription("Removes a condo tower from an appraisal.")
            .WithTags("Block Condo");
    }
}
