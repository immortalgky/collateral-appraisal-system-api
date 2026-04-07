namespace Appraisal.Application.Features.BlockCondo.SaveCondoProject;

public class SaveCondoProjectEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/condo-project",
                async (
                    Guid appraisalId,
                    SaveCondoProjectRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<SaveCondoProjectCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<SaveCondoProjectResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("SaveCondoProject")
            .Produces<SaveCondoProjectResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save condo project")
            .WithDescription("Creates or updates the condo project for an appraisal.")
            .WithTags("Block Condo");
    }
}
