using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.GetAppraisalStatusSummary;

public class GetAppraisalStatusSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/appraisal-status-summary",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetAppraisalStatusSummaryQuery();
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetAppraisalStatusSummary")
            .Produces<GetAppraisalStatusSummaryResult>()
            .WithSummary("Get appraisal status distribution for dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}
