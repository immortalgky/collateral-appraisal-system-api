namespace Appraisal.Application.Features.Project.GetProjectLand;

public class GetProjectLandEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/project/land",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetProjectLandQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);

                    if (result is null)
                        return Results.NoContent();

                    return Results.Ok(result);
                }
            )
            .WithName("GetProjectLand")
            .Produces<GetProjectLandResult>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get project land")
            .WithDescription("Retrieves land details for a LandAndBuilding project. Returns 204 if no land has been saved yet or if the project is Condo.")
            .WithTags("Project");
    }
}
