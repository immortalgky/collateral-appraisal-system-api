namespace Appraisal.Application.Features.Appraisals.AssignPhotoToTopic;

public class AssignPhotoToTopicEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/gallery/{photoId:guid}/assign-topic",
                async (
                    Guid appraisalId,
                    Guid photoId,
                    AssignPhotoToTopicRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AssignPhotoToTopicCommand(
                        photoId,
                        request.PhotoTopicId);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("AssignPhotoToTopic")
            .Produces<AssignPhotoToTopicResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Assign photo to topic")
            .WithDescription("Assigns a gallery photo to a topic, or unassigns it by passing null.")
            .WithTags("PhotoTopic");
    }
}
