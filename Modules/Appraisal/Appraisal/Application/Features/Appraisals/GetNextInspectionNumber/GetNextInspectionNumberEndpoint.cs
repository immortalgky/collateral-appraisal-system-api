using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetNextInspectionNumber;

/// <summary>
/// GET /appraisals/{appraisalId}/next-inspection-number - display-only preview for the request page. The server
/// stamps the real number itself when the appraisal is created; the request carries no inspection number.
/// </summary>
public class GetNextInspectionNumberEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/next-inspection-number",
                async (Guid appraisalId, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new GetNextInspectionNumberQuery(appraisalId), cancellationToken)))
            .WithName("GetNextInspectionNumber")
            .Produces<GetNextInspectionNumberResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get the inspection round a new construction-inspection request copying this appraisal would be")
            .WithTags("Appraisal")
            .RequireAuthorization();
    }
}
