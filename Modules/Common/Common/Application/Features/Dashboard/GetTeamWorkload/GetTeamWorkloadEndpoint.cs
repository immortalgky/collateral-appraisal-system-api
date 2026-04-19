using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.GetTeamWorkload;

public class GetTeamWorkloadEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/team-workload",
                async (
                    DateOnly? from,
                    DateOnly? to,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    if (from.HasValue && to.HasValue && from.Value > to.Value)
                        return Results.Problem("'from' must not be later than 'to'.", statusCode: 400);

                    var query = new GetTeamWorkloadQuery(from, to);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetTeamWorkload")
            .Produces<GetTeamWorkloadResult>()
            .WithSummary("Get team workload distribution for dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}
