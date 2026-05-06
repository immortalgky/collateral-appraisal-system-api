using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.GetQuotationTaskSummary;

public class GetQuotationTaskSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/quotation-task-summary",
                async (
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetQuotationTaskSummaryQuery(), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetQuotationTaskSummary")
            .Produces<GetQuotationTaskSummaryResult>()
            .WithSummary("Get quotation pipeline task counts for intAdmin dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}
