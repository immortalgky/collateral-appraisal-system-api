using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.SystemMetrics.GetSystemMetrics;

public class GetSystemMetricsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/admin/system-metrics",
                async (
                    DateTime? from,
                    DateTime? to,
                    int? buckets,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new GetSystemMetricsFilter(from, to, buckets);
                    var result = await sender.Send(new GetSystemMetricsQuery(filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("AdminGetSystemMetrics")
            .Produces<SystemMetricsDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Admin: Machine health time series")
            .WithDescription("Bucketed CPU/memory/thread-pool/p95/request metrics per instance, plus process restarts, for the given time range. Defaults to last 24h / 60 buckets. Requires LOGS_VIEW permission.")
            .WithTags("SystemMetrics")
            .RequireAuthorization("LogsView");
    }
}
