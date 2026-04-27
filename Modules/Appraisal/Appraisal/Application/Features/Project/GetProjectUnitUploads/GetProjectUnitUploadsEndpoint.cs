namespace Appraisal.Application.Features.Project.GetProjectUnitUploads;

public class GetProjectUnitUploadsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/project/units/uploads",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetProjectUnitUploadsQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result.Uploads);
                }
            )
            .WithName("GetProjectUnitUploads")
            .Produces<List<ProjectUnitUploadDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get project unit upload history")
            .WithDescription("Retrieves the upload batch history for a project.")
            .WithTags("Project");
    }
}
