using Carter;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetPendingEvaluations;

public class GetPendingEvaluationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/pending-evaluations",
                async (
                    [AsParameters] PaginationRequest pagination,
                    string[]? evaluationStatus,
                    string? search,
                    string? sortBy,
                    string? sortDir,
                    string? appraisalCompanyId,
                    string[]? appraisalStatus,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new PendingEvaluationFilter(evaluationStatus, search, sortBy, sortDir, appraisalCompanyId, appraisalStatus);
                    var result = await sender.Send(new GetPendingEvaluationsQuery(pagination, filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetPendingEvaluations")
            .Produces<PaginatedResult<PendingEvaluationDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending Company Evaluation list")
            .WithDescription("Returns pending appraisal company evaluations. Requires any MONITORING:PENDING_EVALUATION* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyPendingEvaluation);
    }
}
