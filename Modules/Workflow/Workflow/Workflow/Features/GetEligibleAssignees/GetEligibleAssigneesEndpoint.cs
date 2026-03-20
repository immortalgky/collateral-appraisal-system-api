namespace Workflow.Workflow.Features.GetEligibleAssignees;

public class GetEligibleAssigneesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workflows/instances/{workflowInstanceId:guid}/activities/{activityId}/eligible-assignees",
                async (Guid workflowInstanceId, string activityId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetEligibleAssigneesQuery(workflowInstanceId, activityId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetEligibleAssignees")
            .WithTags("Workflows");
    }
}
