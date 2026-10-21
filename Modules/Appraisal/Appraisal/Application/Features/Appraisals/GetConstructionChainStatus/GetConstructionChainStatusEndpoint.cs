using Appraisal.Contracts.Appraisals;
using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetConstructionChainStatus;

/// <summary>
/// GET /appraisals/{appraisalId}/construction-chain-status - where construction stands across the whole chain the
/// appraisal belongs to, and whether a new construction-inspection request (06 / 11) may copy it. Display-side twin
/// of the submit-time check in PriorAppraisalSubmissionGuard, which enforces the same answer.
/// </summary>
public class GetConstructionChainStatusEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/construction-chain-status",
                async (Guid appraisalId, ISender sender, CancellationToken cancellationToken) =>
                    await sender.Send(new GetConstructionChainStatusQuery(appraisalId), cancellationToken)
                        is { } status
                        ? Results.Ok(status)
                        : Results.NotFound())
            .WithName("GetConstructionChainStatus")
            .Produces<ConstructionChainStatusResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get the construction progress and inspection-request block status of an appraisal's chain")
            .WithTags("Appraisal")
            .RequireAuthorization();
    }
}
