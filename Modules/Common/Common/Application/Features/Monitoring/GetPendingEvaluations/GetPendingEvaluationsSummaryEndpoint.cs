using Carter;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Monitoring.GetPendingEvaluations;

public class GetPendingEvaluationsSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/pending-evaluations/summary",
                async (
                    string[]? evaluationStatus,
                    string? search,
                    string? appraisalCompanyId,
                    string[]? appraisalStatus,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new PendingEvaluationFilter(evaluationStatus, search, null, null, appraisalCompanyId, appraisalStatus);
                    var result = await sender.Send(new GetPendingEvaluationsSummaryQuery(filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetPendingEvaluationsSummary")
            .Produces<MonitoringSummaryDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending Evaluations summary KPIs")
            .WithDescription("Returns Total count for the Pending Evaluations tab. Bucket fields are null (no OLA data). Requires any MONITORING:PENDING_EVALUATION* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyPendingEvaluation);
    }
}
