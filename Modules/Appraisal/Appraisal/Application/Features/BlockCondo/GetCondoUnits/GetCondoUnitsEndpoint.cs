namespace Appraisal.Application.Features.BlockCondo.GetCondoUnits;

public class GetCondoUnitsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/condo-units",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCondoUnitsQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetCondoUnitsResponse>();
                    return Results.Ok(response);
                }
            )
            .WithName("GetCondoUnits")
            .Produces<GetCondoUnitsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get condo units")
            .WithDescription("Retrieves all condo units for an appraisal with summary data.")
            .WithTags("Block Condo");
    }
}
