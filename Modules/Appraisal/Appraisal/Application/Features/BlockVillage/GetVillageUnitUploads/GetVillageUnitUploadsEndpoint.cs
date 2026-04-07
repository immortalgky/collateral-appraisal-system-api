namespace Appraisal.Application.Features.BlockVillage.GetVillageUnitUploads;

public class GetVillageUnitUploadsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/village-unit-uploads",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(new GetVillageUnitUploadsQuery(appraisalId), cancellationToken);
                    return Results.Ok(result.Adapt<GetVillageUnitUploadsResponse>());
                }
            )
            .WithName("GetVillageUnitUploads")
            .Produces<GetVillageUnitUploadsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get village unit uploads")
            .WithDescription("Gets all upload batches for an appraisal.")
            .WithTags("Block Village");
    }
}
