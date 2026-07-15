using Carter;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Monitoring.GetPendingEvaluationStaff;

public class GetPendingEvaluationStaffEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/pending-evaluations/staff",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetPendingEvaluationStaffQuery(), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetPendingEvaluationStaff")
            .Produces<IReadOnlyList<InternalFollowupStaffOption>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending Evaluation internal followup staff options")
            .WithDescription("Returns the distinct internal followup staff on the Pending Evaluation list, for the filter autocomplete. Requires any MONITORING:PENDING_EVALUATION* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyPendingEvaluation);
    }
}
