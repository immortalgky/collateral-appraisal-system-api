namespace Appraisal.Application.Features.FeeStructures.GetFeeStructures;

public class GetFeeStructuresEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/fee-structures", async (ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetFeeStructuresQuery(), ct);
                return Results.Ok(result);
            })
            .WithName("GetFeeStructures")
            .Produces<IReadOnlyList<FeeStructureDto>>()
            .WithTags("Fee Structure Config")
            .WithSummary("List fee structures")
            .WithDescription("Returns all fee structure tiers ordered by fee code then min selling price.")
            .RequireAuthorization();
    }
}
