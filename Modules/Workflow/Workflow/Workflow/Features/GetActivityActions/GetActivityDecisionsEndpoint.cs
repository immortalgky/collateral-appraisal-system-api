namespace Workflow.Workflow.Features.GetActivityActions;

public class GetActivityActionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workflows/instances/{workflowInstanceId:guid}/activities/{activityId}/actions",
                async (Guid workflowInstanceId, string activityId, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetActivityActionsQuery(workflowInstanceId, activityId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetActivityActions")
            .WithTags("Workflows");
    }
}
