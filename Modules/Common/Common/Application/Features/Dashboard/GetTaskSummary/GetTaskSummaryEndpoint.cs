using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.GetTaskSummary;

public class GetTaskSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/task-summary",
                async (
                    string? period,
                    DateOnly? from,
                    DateOnly? to,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetTaskSummaryQuery(period ?? "monthly", from, to);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetTaskSummary")
            .Produces<GetTaskSummaryResult>()
            .WithSummary("Get task status summary for current user")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}
