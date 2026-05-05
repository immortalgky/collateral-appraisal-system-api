using Carter;
using MediatR;

namespace Appraisal.Application.Features.ConstructionInspections.GetWorkDetailsForPrefill;

/// <summary>
/// GET /appraisal/construction-inspections/{inspectionId}/work-details
///
/// Returns the work details of a prior ConstructionInspection so the FE can seed
/// PreviousProgressPct per item when starting a Progressive appraisal.
///
/// Auth: authenticated user (no admin gate required).
/// </summary>
public class GetWorkDetailsForPrefillEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisal/construction-inspections/{inspectionId:guid}/work-details",
                async (
                    Guid inspectionId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetWorkDetailsForPrefillQuery(inspectionId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("GetConstructionInspectionWorkDetails")
            .Produces<GetWorkDetailsForPrefillResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Get prior construction inspection work details for Progressive appraisal prefill")
            .WithDescription(
                "Returns the work details of a prior ConstructionInspection. " +
                "The FE seeds the new inspection's PreviousProgressPct from CurrentProgressPct, " +
                "matched by ConstructionWorkItemId (template FK) then WorkItemName as fallback.")
            .WithTags("ConstructionInspections")
            .RequireAuthorization();
    }
}
