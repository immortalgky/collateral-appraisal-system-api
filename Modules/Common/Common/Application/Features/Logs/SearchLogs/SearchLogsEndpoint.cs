using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Logs.SearchLogs;

public class SearchLogsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/admin/logs",
                async (
                    string? q,
                    DateTime? from,
                    DateTime? to,
                    string? levels,
                    long? beforeId,
                    long? afterId,
                    int? pageSize,
                    string? sortDir,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new SearchLogsFilter(q, from, to, levels, beforeId, afterId, pageSize, sortDir);
                    var result = await sender.Send(new SearchLogsQuery(filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("AdminSearchLogs")
            .Produces<SearchLogsResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Admin: Search application logs")
            .WithDescription("Returns application logs stored in dbo.Logs, cursor-paginated by Id. Filterable by free-text/field query (see LogQueryParser), level, and a time range (defaults to the last 24h, capped at 31 days). Requires LOGS_VIEW permission.")
            .WithTags("Logs")
            .RequireAuthorization("LogsView");
    }
}
