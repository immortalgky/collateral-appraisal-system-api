using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.SystemMetrics.GetCurrentSystemMetrics;

public class GetCurrentSystemMetricsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/admin/system-metrics/current",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetCurrentSystemMetricsQuery(), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("AdminGetCurrentSystemMetrics")
            .Produces<IReadOnlyList<SystemMetricsCurrentDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Admin: Current machine health")
            .WithDescription("Latest sample per instance reporting within the last 10 minutes, plus each instance's Gen2 collections over the last hour. Requires LOGS_VIEW permission.")
            .WithTags("SystemMetrics")
            .RequireAuthorization("LogsView");
    }
}
