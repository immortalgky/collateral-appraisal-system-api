namespace Appraisal.Application.Features.Appraisals.GetPhotoTopics;

public class GetPhotoTopicsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/photo-topics",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetPhotoTopicsQuery(appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("GetPhotoTopics")
            .Produces<GetPhotoTopicsResult>(StatusCodes.Status200OK)
            .WithSummary("Get photo topics with photos")
            .WithDescription("Returns all photo topics for the appraisal with their assigned photos, ordered by sort order.")
            .WithTags("PhotoTopic");
    }
}
