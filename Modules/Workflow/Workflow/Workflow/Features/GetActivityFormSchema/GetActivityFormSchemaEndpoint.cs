namespace Workflow.Workflow.Features.GetActivityFormSchema;

public class GetActivityFormSchemaEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workflows/instances/{workflowInstanceId:guid}/activities/{activityId}/form-schema",
                async (Guid workflowInstanceId, string activityId, ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetActivityFormSchemaQuery(workflowInstanceId, activityId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetActivityFormSchema")
            .WithTags("Workflows");
    }
}
