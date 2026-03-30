using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.GetRecentTasks;

public class GetRecentTasksEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/recent-tasks",
                async (
                    int? limit,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetRecentTasksQuery(limit ?? 10);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetRecentTasks")
            .Produces<GetRecentTasksResult>()
            .WithSummary("Get recent tasks for current user")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}
