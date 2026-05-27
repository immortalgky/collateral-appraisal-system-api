using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalMapPins;

public class GetAppraisalMapPinsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/map-pins",
                async (Guid appraisalId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetAppraisalMapPinsQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetAppraisalMapPins")
            .Produces<GetAppraisalMapPinsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get map pins for an appraisal")
            .WithDescription(
                "Returns the appraisal's own collateral property locations (land and condo " +
                "properties that carry geo-coordinates) and its linked market-comparable pins. " +
                "Intended for the 360-summary map view. The in-progress appraisal is excluded " +
                "from POST /history-search (which requires CompletedAt IS NOT NULL), so this " +
                "dedicated endpoint serves the map-center and nearby-radius context.")
            .WithTags("Appraisal")
            .RequireAuthorization("history-search.view");
    }
}
