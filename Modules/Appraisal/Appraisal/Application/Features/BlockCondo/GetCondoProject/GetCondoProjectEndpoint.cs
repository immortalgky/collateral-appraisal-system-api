namespace Appraisal.Application.Features.BlockCondo.GetCondoProject;

public class GetCondoProjectEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/condo-project",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCondoProjectQuery(appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    if (result is null)
                        return Results.NoContent();

                    var response = result.Adapt<GetCondoProjectResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetCondoProject")
            .Produces<GetCondoProjectResponse>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get condo project")
            .WithDescription("Retrieves the condo project for an appraisal.")
            .WithTags("Block Condo");
    }
}
