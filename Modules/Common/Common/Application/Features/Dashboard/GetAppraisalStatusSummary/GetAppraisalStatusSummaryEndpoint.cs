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
                async (
                    DateOnly? from,
                    DateOnly? to,
                    string? assigneeId,
                    string? bankingSegment,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    if (from.HasValue && to.HasValue && from.Value > to.Value)
                        return Results.Problem("'from' must not be later than 'to'.", statusCode: 400);

                    var query = new GetAppraisalStatusSummaryQuery(from, to, assigneeId, bankingSegment);
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
