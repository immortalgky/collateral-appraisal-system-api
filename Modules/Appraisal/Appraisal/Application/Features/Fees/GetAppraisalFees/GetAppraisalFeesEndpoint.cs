namespace Appraisal.Application.Features.Fees.GetAppraisalFees;

public class GetAppraisalFeesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/fees",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetAppraisalFeesQuery(appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(new GetAppraisalFeesResponse(result.Fees));
                }
            )
            .WithName("GetAppraisalFees")
            .Produces<GetAppraisalFeesResponse>(StatusCodes.Status200OK)
            .WithSummary("Get appraisal fees")
            .WithDescription("Get all fees and their items for an appraisal.")
            .WithTags("Fee");
    }
}
