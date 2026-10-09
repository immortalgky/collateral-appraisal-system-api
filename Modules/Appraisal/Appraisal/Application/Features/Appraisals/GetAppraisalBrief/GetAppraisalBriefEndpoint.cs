using Carter;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalBrief;

public class GetAppraisalBriefEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/brief",
                async (Guid appraisalId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetAppraisalBriefQuery(appraisalId), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetAppraisalBrief")
            .Produces<GetAppraisalBriefResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Collateral brief for the credit-side tracking screen")
            .WithDescription(
                "Returns the appraisal header, the collateral items, who is holding the work and "
                + "how to reach them, and — for a tracking-only caller, only once the committee has "
                + "approved the price — the appraised/forced-sale/insurance values and the document "
                + "list. Carries the SLA DUE DATE, the current holder's contact details and how "
                + "long they have held the task; it does not carry the assignment history or the "
                + "SLA status ladder.")
            .WithTags("Appraisal")
            // Open to both audiences: credit reaches it on APPRAISAL_TRACKING_VIEW, and an
            // internal user holding APPRAISAL_VIEW can open the same screen (it is a useful
            // summary in its own right). The release rule is enforced in the handler either way.
            .RequireAuthorization("appraisal.browse");
    }
}
