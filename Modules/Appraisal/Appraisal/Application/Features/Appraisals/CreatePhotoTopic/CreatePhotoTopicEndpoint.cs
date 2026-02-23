namespace Appraisal.Application.Features.Appraisals.CreatePhotoTopic;

public class CreatePhotoTopicEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/photo-topics",
                async (
                    Guid appraisalId,
                    CreatePhotoTopicRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CreatePhotoTopicCommand(
                        appraisalId,
                        request.TopicName,
                        request.SortOrder,
                        request.DisplayColumns);

                    var result = await sender.Send(command, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("CreatePhotoTopic")
            .Produces<CreatePhotoTopicResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create a photo topic")
            .WithDescription("Creates a new photo topic for organizing gallery photos in the appraisal.")
            .WithTags("PhotoTopic");
    }
}
