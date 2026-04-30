using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Project.AddProjectTowerImage;

/// <summary>
/// Endpoint: POST /appraisals/{appraisalId}/project/towers/{towerId}/images
/// Adds an image to a project tower by linking a gallery photo.
/// </summary>
public class AddProjectTowerImageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/project/towers/{towerId:guid}/images",
                async (
                    Guid appraisalId,
                    Guid towerId,
                    AddProjectTowerImageRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AddProjectTowerImageCommand(
                        towerId,
                        request.GalleryPhotoId,
                        request.Title,
                        request.Description);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<AddProjectTowerImageResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("AddProjectTowerImage")
            .Produces<AddProjectTowerImageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add image to project tower")
            .WithDescription("Links a gallery photo as an image to a project tower.")
            .WithTags("ProjectTowers");
    }
}
