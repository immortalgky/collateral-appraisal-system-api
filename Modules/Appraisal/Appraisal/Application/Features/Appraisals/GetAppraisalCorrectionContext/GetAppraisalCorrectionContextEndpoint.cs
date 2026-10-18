using Appraisal.Application.Features.Appraisals.NotifyExternalSystem;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalCorrectionContext;

/// <summary>
/// GET /appraisals/{appraisalId}/correction-context — what the data-correction page header needs that
/// GetAppraisalById does not carry: whether to offer any "notify source system" action, and the approval
/// time. externalSystem is null when there is nobody the webhook would send to (see AppraisalExternalSourceQuery).
/// </summary>
public class GetAppraisalCorrectionContextEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/correction-context",
                async (
                    Guid appraisalId,
                    ISqlConnectionFactory connectionFactory,
                    CancellationToken cancellationToken
                ) =>
                {
                    var source = await AppraisalExternalSourceQuery.GetAsync(
                                     connectionFactory.GetOpenConnection(), appraisalId, cancellationToken)
                                 ?? throw new NotFoundException("Appraisal", appraisalId);

                    return Results.Ok(source);
                }
            )
            .WithName("GetAppraisalCorrectionContext")
            .Produces<ExternalSource>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Source system (if it would be notified) and approval time for the data-correction page")
            .WithDescription(
                "externalSystem is non-null only when the APPRAISAL_COMPLETED webhook would really send: the " +
                "appraisal has a number, its request has an ExternalCaseKey and an ExternalSystem, and that " +
                "system has an active APPRAISAL_COMPLETED (or catch-all) webhook subscription.")
            .WithTags("Appraisal Data Correction")
            .RequireAuthorization("appraisal.data-correction");
    }
}
