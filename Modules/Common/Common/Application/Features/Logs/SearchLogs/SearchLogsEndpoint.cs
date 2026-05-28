using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Common.Application.Features.Logs.SearchLogs;

public class SearchLogsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/admin/logs",
                async (
                    [AsParameters] PaginationRequest pagination,
                    string? level,
                    string? correlationId,
                    string? appraisalId,
                    string? requestId,
                    string? entityId,
                    string? workflowInstanceId,
                    string? collateralId,
                    string? documentId,
                    string? search,
                    DateTime? from,
                    DateTime? to,
                    string? sortDir,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new SearchLogsFilter(
                        level, correlationId, appraisalId, requestId,
                        entityId, workflowInstanceId, collateralId, documentId,
                        search, from, to, sortDir);

                    var result = await sender.Send(new SearchLogsQuery(pagination, filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("AdminSearchLogs")
            .Produces<PaginatedResult<LogDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Admin: Search application logs")
            .WithDescription("Returns paginated application logs stored in dbo.Logs. Filterable by level, business IDs, message text, and time range. Requires LOGS_VIEW permission.")
            .WithTags("Logs")
            .RequireAuthorization("LogsView");
    }
}
