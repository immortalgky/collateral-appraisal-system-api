using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Logs.GetLogSummary;

public class GetLogSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/admin/logs/summary",
                async (
                    string? q,
                    DateTime? from,
                    DateTime? to,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new GetLogSummaryFilter(q, from, to);
                    var result = await sender.Send(new GetLogSummaryQuery(filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("AdminGetLogSummary")
            .Produces<LogSummaryDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Admin: Application log summary")
            .WithDescription("Level counts, a 48-bucket histogram, and the top 6 recurring Warning/Error/Fatal problems for the same q/from/to filter as SearchLogs. Requires LOGS_VIEW permission.")
            .WithTags("Logs")
            .RequireAuthorization("LogsView");
    }
}
