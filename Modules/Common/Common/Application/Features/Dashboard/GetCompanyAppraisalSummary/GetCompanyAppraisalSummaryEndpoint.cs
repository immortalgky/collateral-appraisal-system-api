using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.GetCompanyAppraisalSummary;

public class GetCompanyAppraisalSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/company-appraisal-summary",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetCompanyAppraisalSummaryQuery();
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetCompanyAppraisalSummary")
            .Produces<GetCompanyAppraisalSummaryResult>()
            .WithSummary("Get per-company appraisal assignment summary")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}
