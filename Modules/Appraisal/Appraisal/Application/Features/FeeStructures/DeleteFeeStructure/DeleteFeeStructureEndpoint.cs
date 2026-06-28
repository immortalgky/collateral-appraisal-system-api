namespace Appraisal.Application.Features.FeeStructures.DeleteFeeStructure;

public class DeleteFeeStructureEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/fee-structures/{id:guid}", async (
                Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteFeeStructureCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("DeleteFeeStructure")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Fee Structure Config")
            .WithSummary("Delete a fee structure")
            .WithDescription("Deletes a fee structure tier by id.")
            .RequireAuthorization();
    }
}
