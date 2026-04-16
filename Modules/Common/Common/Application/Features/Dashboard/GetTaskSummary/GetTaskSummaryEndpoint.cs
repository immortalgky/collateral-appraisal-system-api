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
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetTaskSummaryQuery(), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetTaskSummary")
            .Produces<GetTaskSummaryResult>()
            .WithSummary("Get task status counts for current user")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}
