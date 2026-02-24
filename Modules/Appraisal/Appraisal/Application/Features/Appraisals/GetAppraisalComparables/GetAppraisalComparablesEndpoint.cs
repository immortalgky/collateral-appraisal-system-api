namespace Appraisal.Application.Features.Appraisals.GetAppraisalComparables;

public class GetAppraisalComparablesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/appraisals/{appraisalId:guid}/comparables",
                async (Guid appraisalId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetAppraisalComparablesQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetAppraisalComparablesResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetAppraisalComparables")
            .Produces<GetAppraisalComparablesResponse>(StatusCodes.Status200OK)
            .WithSummary("Get all comparables linked to an appraisal")
            .WithDescription(
                "Returns all market comparables linked to the specified appraisal, including their adjustments.")
            .WithTags("AppraisalComparables");
    }
}