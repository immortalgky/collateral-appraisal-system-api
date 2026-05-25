using Carter;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Monitoring.GetTopBreaches;

public class GetTopBreachesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/top-breaches",
                async (
                    int? limit,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new GetTopBreachesQuery(limit ?? 5),
                        cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetTopBreaches")
            .Produces<IReadOnlyList<TopBreachDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Top breached tasks")
            .WithDescription(
                "Returns the top N most-overdue tasks (OlaVarianceHours DESC) across Internal, External, " +
                "and Followup queues. Only actively breached rows (OlaVarianceHours > 0) are included. " +
                "Results are filtered to sections the caller has permission to see. " +
                "Limit defaults to 5; maximum is 50. " +
                "Requires any MONITORING:PENDING_INTERNAL:*, MONITORING:PENDING_EXTERNAL:*, " +
                "or MONITORING:PENDING_FOLLOWUP permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyTopBreaches);
    }
}
