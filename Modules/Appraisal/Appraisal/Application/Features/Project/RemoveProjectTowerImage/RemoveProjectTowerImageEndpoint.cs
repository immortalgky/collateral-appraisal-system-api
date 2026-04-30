using Carter;
using MediatR;

namespace Appraisal.Application.Features.Project.RemoveProjectTowerImage;

/// <summary>
/// Endpoint: DELETE /appraisals/{appraisalId}/project/towers/{towerId}/images/{imageId}
/// Removes an image from a project tower.
/// </summary>
public class RemoveProjectTowerImageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/appraisals/{appraisalId:guid}/project/towers/{towerId:guid}/images/{imageId:guid}",
                async (
                    Guid appraisalId,
                    Guid towerId,
                    Guid imageId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RemoveProjectTowerImageCommand(towerId, imageId);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("RemoveProjectTowerImage")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove image from project tower")
            .WithDescription("Removes an image from a project tower.")
            .WithTags("ProjectTowers");
    }
}
