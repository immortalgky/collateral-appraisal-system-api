namespace Appraisal.Application.Features.Appraisals.UpdatePhotoTopic;

public class UpdatePhotoTopicEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/photo-topics/{topicId:guid}",
                async (
                    Guid appraisalId,
                    Guid topicId,
                    UpdatePhotoTopicRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UpdatePhotoTopicCommand(
                        topicId,
                        request.TopicName,
                        request.SortOrder,
                        request.DisplayColumns);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("UpdatePhotoTopic")
            .Produces<UpdatePhotoTopicResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Update a photo topic")
            .WithDescription("Updates an existing photo topic's name, sort order, and display columns.")
            .WithTags("PhotoTopic");
    }
}
