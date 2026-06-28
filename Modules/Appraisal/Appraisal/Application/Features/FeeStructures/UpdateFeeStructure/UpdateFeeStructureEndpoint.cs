namespace Appraisal.Application.Features.FeeStructures.UpdateFeeStructure;

public record UpdateFeeStructureRequest(
    decimal BaseAmount,
    decimal MinSellingPrice,
    decimal? MaxSellingPrice,
    bool IsActive);

public class UpdateFeeStructureEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/fee-structures/{id:guid}", async (
                Guid id, UpdateFeeStructureRequest request, ISender sender, CancellationToken ct) =>
            {
                var command = request.Adapt<UpdateFeeStructureCommand>() with { Id = id };
                var result = await sender.Send(command, ct);
                return Results.Ok(result);
            })
            .WithName("UpdateFeeStructure")
            .Produces<FeeStructureDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Fee Structure Config")
            .WithSummary("Update a fee structure")
            .WithDescription("Updates a fee structure tier (fee code is immutable). The range may not overlap an existing active tier of the same code.")
            .RequireAuthorization();
    }
}
