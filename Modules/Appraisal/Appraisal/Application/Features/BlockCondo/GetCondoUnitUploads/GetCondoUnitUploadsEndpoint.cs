namespace Appraisal.Application.Features.BlockCondo.GetCondoUnitUploads;

public class GetCondoUnitUploadsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/condo-unit-uploads",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCondoUnitUploadsQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetCondoUnitUploadsResponse>();
                    return Results.Ok(response);
                }
            )
            .WithName("GetCondoUnitUploads")
            .Produces<GetCondoUnitUploadsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get condo unit uploads")
            .WithDescription("Retrieves all condo unit upload history for an appraisal.")
            .WithTags("Block Condo");
    }
}
