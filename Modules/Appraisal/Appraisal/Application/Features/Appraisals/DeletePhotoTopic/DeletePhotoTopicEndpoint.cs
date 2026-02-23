namespace Appraisal.Application.Features.Appraisals.DeletePhotoTopic;

public class DeletePhotoTopicEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/appraisals/{appraisalId:guid}/photo-topics/{topicId:guid}",
                async (
                    Guid appraisalId,
                    Guid topicId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new DeletePhotoTopicCommand(topicId);

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("DeletePhotoTopic")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Delete a photo topic")
            .WithDescription("Deletes a photo topic. Fails if the topic still has photos assigned.")
            .WithTags("PhotoTopic");
    }
}
