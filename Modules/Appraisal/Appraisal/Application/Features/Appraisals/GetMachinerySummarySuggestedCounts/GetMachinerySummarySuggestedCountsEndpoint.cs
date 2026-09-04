namespace Appraisal.Application.Features.Appraisals.GetMachinerySummarySuggestedCounts;

public class GetMachinerySummarySuggestedCountsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/machinery-summary/suggested-counts",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetMachinerySummarySuggestedCountsQuery(appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetMachinerySummarySuggestedCountsResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetMachinerySummarySuggestedCounts")
            .Produces<GetMachinerySummarySuggestedCountsResponse>()
            .WithSummary("Get suggested machinery summary counts")
            .WithDescription(
                "Derives the Section 3.1 head-counts from the machines recorded on the appraisal. "
                + "Suggestions only: the stored summary keeps whatever the appraiser typed. "
                + "Returns zeroes when the appraisal has no machinery.")
            .WithTags("Appraisal Properties");
    }
}
