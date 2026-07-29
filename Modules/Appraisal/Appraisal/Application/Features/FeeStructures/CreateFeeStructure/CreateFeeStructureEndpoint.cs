namespace Appraisal.Application.Features.FeeStructures.CreateFeeStructure;

public record CreateFeeStructureRequest(
    string FeeCode,
    decimal BaseAmount,
    decimal MinSellingPrice,
    decimal? MaxSellingPrice,
    bool IsActive = true,
    // Omit or send null to create a tier on the generic ladder that applies to any appraisal type.
    string? AppraisalType = null);

public class CreateFeeStructureEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/fee-structures", async (
                CreateFeeStructureRequest request, ISender sender, CancellationToken ct) =>
            {
                var command = request.Adapt<CreateFeeStructureCommand>();
                var result = await sender.Send(command, ct);
                return Results.Created($"/api/fee-structures/{result.Id}", result);
            })
            .WithName("CreateFeeStructure")
            .Produces<FeeStructureDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Fee Structure Config")
            .WithSummary("Create a fee structure")
            .WithDescription("Creates a fee structure tier. The fee code must exist in the TypeOfFee parameter group and may not overlap an existing active tier in the same ladder (same fee code and appraisal-type scope). Leave appraisalType null for the generic ladder used by any appraisal type.")
            .RequireAuthorization();
    }
}
