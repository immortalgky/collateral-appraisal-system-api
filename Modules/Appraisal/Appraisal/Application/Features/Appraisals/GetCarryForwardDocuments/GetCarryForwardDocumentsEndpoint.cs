using Appraisal.Contracts.Appraisals;
using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetCarryForwardDocuments;

/// <summary>
/// GET /appraisals/{appraisalId}/carry-forward-documents
///
/// The files a new request that references this appraisal can reuse (prior request files plus the
/// summary report as D036). Returns 404 if the appraisal does not exist, 409 if it is not Completed.
/// </summary>
public class GetCarryForwardDocumentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/carry-forward-documents",
                async (Guid appraisalId, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new GetCarryForwardDocumentsQuery(appraisalId, EnforceCallerScope: true), cancellationToken)))
            .WithName("GetCarryForwardDocuments")
            .Produces<CarryForwardDocumentsResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Get documents a new request can carry forward from a completed appraisal")
            .WithTags("Appraisal")
            .RequireAuthorization();
    }
}
