using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.ReconcileAppraisalCounts;

public class ReconcileAppraisalCountsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/dashboard/reconcile-appraisal-counts",
                async (
                    ReconcileAppraisalCountsRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var command = new ReconcileAppraisalCountsCommand(request.FromDate, request.ToDate);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("ReconcileAppraisalCounts")
            .Produces<ReconcileAppraisalCountsResult>()
            .WithSummary("Reconcile DailyAppraisalCounts from source tables for a date range")
            .WithTags("Dashboard")
            .RequireAuthorization("CanManageRoles");
    }
}
