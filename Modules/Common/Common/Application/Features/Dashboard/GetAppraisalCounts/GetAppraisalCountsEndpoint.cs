using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.GetAppraisalCounts;

public class GetAppraisalCountsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/appraisal-counts",
                async (
                    string? period,
                    DateOnly? from,
                    DateOnly? to,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var query = new GetAppraisalCountsQuery(period ?? "monthly", from, to);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetAppraisalCounts")
            .Produces<GetAppraisalCountsResult>()
            .WithSummary("Get appraisal count trends for dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}
