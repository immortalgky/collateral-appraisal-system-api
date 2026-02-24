using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.UnlinkAppraisalComparable;

public class UnlinkAppraisalComparableEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/appraisals/{appraisalId:guid}/comparables/{comparableId:guid}",
            async (Guid appraisalId, Guid comparableId, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new UnlinkAppraisalComparableCommand(appraisalId, comparableId);
                await sender.Send(command, cancellationToken);
                return Results.NoContent();
            })
            .WithName("UnlinkAppraisalComparable")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Unlink a market comparable from an appraisal")
            .WithDescription("Removes the link between a market comparable and an appraisal.")
            .WithTags("AppraisalComparables");
    }
}
