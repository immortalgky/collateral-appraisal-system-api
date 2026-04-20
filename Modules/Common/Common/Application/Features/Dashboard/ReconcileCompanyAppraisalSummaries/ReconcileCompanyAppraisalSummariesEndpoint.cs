using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.ReconcileCompanyAppraisalSummaries;

public class ReconcileCompanyAppraisalSummariesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/dashboard/reconcile-company-appraisal-summaries",
                async (
                    ReconcileCompanyAppraisalSummariesRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new ReconcileCompanyAppraisalSummariesCommand(request.FromDate, request.ToDate);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("ReconcileCompanyAppraisalSummaries")
            .Produces<ReconcileCompanyAppraisalSummariesResult>()
            .WithSummary("Reconcile CompanyAppraisalSummaries from source tables for a date range")
            .WithTags("Dashboard")
            .RequireAuthorization("CanManageRoles");
    }
}
