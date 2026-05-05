using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalCopyTemplate;

/// <summary>
/// GET /appraisals/{appraisalId}/copy-template
///
/// Returns a copyable snapshot of a completed appraisal's request data so that
/// the front-end can pre-fill a new CreateRequest form without retyping everything.
///
/// Returns 404 if the appraisal does not exist.
/// Returns 409 if the appraisal exists but is not in Completed status.
/// </summary>
public class GetAppraisalCopyTemplateEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/copy-template",
                async (Guid appraisalId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetAppraisalCopyTemplateQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetAppraisalCopyTemplate")
            .Produces<AppraisalCopyTemplateDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Get appraisal copy template")
            .WithDescription(
                "Returns the copyable request data from a completed appraisal. " +
                "Appointment and fee sections are excluded. " +
                "Returns 404 if not found; 409 if the appraisal is not Completed.")
            .WithTags("Appraisal");
    }
}
