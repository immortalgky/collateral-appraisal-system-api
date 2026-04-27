namespace Appraisal.Application.Features.Project.GetProjectUnits;

public class GetProjectUnitsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/project/units",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetProjectUnitsQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("GetProjectUnits")
            .Produces<GetProjectUnitsResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get project units")
            .WithDescription("Retrieves all uploaded units for a project.")
            .WithTags("Project");
    }
}
