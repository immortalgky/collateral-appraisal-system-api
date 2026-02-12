namespace Workflow.Tasks.Features.GetTasks;

public class GetTasksEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/tasks", async (ISender sender) =>
            {
                var result = await sender.Send(new GetTasksCommand());
                return Results.Ok(new GetTasksResponse(result));
            })
            .WithName("GetTasks")
            .Produces<GetTasksResponse>()
            .WithSummary("Get all tasks")
            .WithDescription("Retrieves all workflow tasks with pagination support.")
            .WithTags("Tasks");
    }
}