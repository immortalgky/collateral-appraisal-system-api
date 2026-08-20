using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetPreviousAppraisalChain;

public class GetPreviousAppraisalChainEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/previous-chain",
                async (Guid appraisalId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetPreviousAppraisalChainQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetPreviousAppraisalChain")
            .Produces<GetPreviousAppraisalChainResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get the chain of previous appraisals for an appraisal")
            .WithDescription(
                "Walks appraisal.Appraisals.PrevAppraisalId ancestor links and returns the chain " +
                "of prior appraisals, nearest ancestor first. The queried appraisal itself is " +
                "excluded. The walk is not depth-limited — it stops only on a cycle — so a " +
                "collateral inspected many times returns its full history. Intended for the " +
                "360-summary page.")
            .WithTags("Appraisal")
            .RequireAuthorization();
    }
}
