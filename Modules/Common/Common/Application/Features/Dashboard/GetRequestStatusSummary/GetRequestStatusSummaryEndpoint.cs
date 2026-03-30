using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.GetRequestStatusSummary;

public class GetRequestStatusSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/request-status-summary",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetRequestStatusSummaryQuery();
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetRequestStatusSummary")
            .Produces<GetRequestStatusSummaryResult>()
            .WithSummary("Get request status distribution for dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}
