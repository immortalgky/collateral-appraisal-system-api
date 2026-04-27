namespace Workflow.Tasks.Features.GetTaskCounts;

public class GetTaskCountsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/tasks/counts",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetTaskCountsQuery(), cancellationToken);
                    return Results.Ok(new GetTaskCountsResponse(result.Counts));
                }
            )
            .WithName("GetTaskCounts")
            .Produces<GetTaskCountsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get per-activity My/Pool task counts for the current user")
            .WithDescription(
                "Returns one row per ActivityId with the count of My (direct user assignment) and Pool (group/team assignment) tasks. Activities with zero in both buckets are omitted.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}
