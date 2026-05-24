namespace Appraisal.Application.Features.Project.SaveProjectDraft;

public class SaveProjectDraftEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/project/draft",
                async (
                    Guid appraisalId,
                    SaveProjectDraftRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<SaveProjectDraftCommand>()
                        with { AppraisalId = appraisalId };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<SaveProjectDraftResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("SaveProjectDraft")
            .Produces<SaveProjectDraftResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save project draft")
            .WithDescription("Creates or updates the project for an appraisal with relaxed validation (required business fields are not enforced). ProjectType discriminates Condo vs LandAndBuilding.")
            .WithTags("Project");
    }
}
