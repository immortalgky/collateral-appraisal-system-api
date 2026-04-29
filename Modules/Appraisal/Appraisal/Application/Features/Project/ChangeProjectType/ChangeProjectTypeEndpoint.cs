using Appraisal.Application.Features.Project.GetProject;

namespace Appraisal.Application.Features.Project.ChangeProjectType;

/// <summary>
/// POST /appraisals/{appraisalId:guid}/project:change-type
///
/// Atomically destroys the existing Project and recreates it with the requested ProjectType,
/// preserving shared fields. Returns the new project in GetProjectResponse shape so the
/// frontend can hydrate without a follow-up GET.
///
/// 200 — Success (new project payload)
/// 400 — Validation failure or same type requested
/// 404 — No project found for the given appraisalId
/// </summary>
public class ChangeProjectTypeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/project:change-type",
                async (
                    Guid appraisalId,
                    ChangeProjectTypeRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new ChangeProjectTypeCommand(
                        AppraisalId:    appraisalId,
                        NewProjectType: request.NewProjectType);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<GetProjectResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("ChangeProjectType")
            .Produces<GetProjectResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Change project type")
            .WithDescription(
                "Atomically replaces the Project aggregate with a new one of the specified type. " +
                "All child data (towers, models, units, pricing assumptions, land) is destroyed. " +
                "Shared fields (name, developer, location, etc.) are preserved. " +
                "Returns 404 if no project exists; 400 if the new type matches the current type.")
            .WithTags("Project");
    }
}
