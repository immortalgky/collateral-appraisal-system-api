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
                    DateOnly? from,
                    DateOnly? to,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    if (from.HasValue && to.HasValue && from.Value > to.Value)
                        return Results.Problem("'from' must not be later than 'to'.", statusCode: 400);

                    var result = await sender.Send(new GetTaskSummaryQuery(from, to), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetTaskSummary")
            .Produces<GetTaskSummaryResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Get task status counts for current user within an optional date range")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}
