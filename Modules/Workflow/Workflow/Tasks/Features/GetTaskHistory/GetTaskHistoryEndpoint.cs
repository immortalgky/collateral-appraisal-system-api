namespace Workflow.Tasks.Features.GetTaskHistory;

public class GetTaskHistoryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/workflows/instances/{workflowInstanceId:guid}/task-history",
                async (
                    Guid workflowInstanceId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetTaskHistoryQuery(workflowInstanceId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetWorkflowInstanceTaskHistory")
            .Produces<GetTaskHistoryResponse>()
            .WithSummary("Get task history for a workflow instance")
            .WithDescription(
                "Returns the merged completed + currently-pending task list for a workflow instance, " +
                "ordered by AssigneeAssignedAt (when each row's holder received the task) so a " +
                "reassigned task's rows follow the real hand-off order. Pending tasks have null " +
                "CompletedAt/ActionTaken/Remark.")
            .WithTags("Tasks")
            .RequireAuthorization();
    }
}
