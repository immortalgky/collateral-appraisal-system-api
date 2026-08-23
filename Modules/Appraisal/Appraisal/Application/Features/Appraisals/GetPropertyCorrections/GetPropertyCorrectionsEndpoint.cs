namespace Appraisal.Application.Features.Appraisals.GetPropertyCorrections;

public class GetPropertyCorrectionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/property-corrections",
                async (
                    Guid appraisalId,
                    Guid? propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new GetPropertyCorrectionsQuery(appraisalId, propertyId), cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("GetPropertyCorrections")
            .Produces<GetPropertyCorrectionsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Correction history for an appraisal")
            .WithDescription(
                "Who changed which field, from what to what, and why — newest first. " +
                "Pass propertyId to narrow it to a single property.")
            .WithTags("Appraisal Data Correction")
            .RequireAuthorization("appraisal.data-correction");
    }
}
