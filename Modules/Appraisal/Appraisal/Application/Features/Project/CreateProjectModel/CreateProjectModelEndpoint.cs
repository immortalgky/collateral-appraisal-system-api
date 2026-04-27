namespace Appraisal.Application.Features.Project.CreateProjectModel;

public class CreateProjectModelEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/project/models",
                async (
                    Guid appraisalId,
                    CreateProjectModelRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateProjectModelCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateProjectModelResponse>();

                    return Results.Created($"/appraisals/{appraisalId}/project/models/{response.Id}", response);
                }
            )
            .WithName("CreateProjectModel")
            .Produces<CreateProjectModelResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create project model")
            .WithDescription("Creates a new model within a project. Supported for both Condo and LandAndBuilding projects.")
            .WithTags("Project");
    }
}
