using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Project.AddProjectModelImage;

/// <summary>
/// Endpoint: POST /appraisals/{appraisalId}/project/models/{modelId}/images
/// Adds an image to a project model by linking a gallery photo.
/// </summary>
public class AddProjectModelImageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/project/models/{modelId:guid}/images",
                async (
                    Guid appraisalId,
                    Guid modelId,
                    AddProjectModelImageRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AddProjectModelImageCommand(
                        modelId,
                        request.GalleryPhotoId,
                        request.Title,
                        request.Description);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<AddProjectModelImageResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("AddProjectModelImage")
            .Produces<AddProjectModelImageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add image to project model")
            .WithDescription("Links a gallery photo as an image to a project model.")
            .WithTags("ProjectModels");
    }
}
