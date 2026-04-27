namespace Appraisal.Application.Features.Project.CreateProjectTower;

public class CreateProjectTowerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/project/towers",
                async (
                    Guid appraisalId,
                    CreateProjectTowerRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateProjectTowerCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateProjectTowerResponse>();

                    return Results.Created($"/appraisals/{appraisalId}/project/towers/{response.Id}", response);
                }
            )
            .WithName("CreateProjectTower")
            .Produces<CreateProjectTowerResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create project tower")
            .WithDescription("Creates a tower within a Condo project. Returns 500 if the project is not a Condo project.")
            .WithTags("Project");
    }
}
