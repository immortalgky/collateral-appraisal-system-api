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
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetTeamWorkloadQuery();
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
