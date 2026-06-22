using Carter;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetAssetSummary;

public class GetAssetSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/asset-summary",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetAssetSummaryQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);

                    if (result.Items is null)
                        return Results.NotFound();

                    return Results.Ok(new GetAssetSummaryResponse(result.Items
                            .Select(i => new AssetSummaryItemResponse(
                                i.Id,
                                i.PropertyType,
                                i.AssetDetail,
                                i.Area,
                                i.PricePerUnit,
                                i.EstimatedPrice,
                                i.CurrentPrice,
                                i.GroupSet,
                                i.IsPricesCurrent))
                            .ToList(),
                        result.Groups!
                            .Select(g => new AssetSummaryGroupResponse(
                                g.Id,
                                g.GroupSet,
                                g.AssetGroupDetail,
                                g.SumEstimatedPrice,
                                g.RoundEstimatedPrice,
                                g.SumCurrentPrice,
                                g.RoundCurrentPrice))
                            .ToList()));
                })
            .WithName("GetAssetSummary")
            .Produces<GetAssetSummaryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get asset summary for a migrated appraisal")
            .WithDescription(
                "Returns legacy asset summary items and group totals. " +
                "Returns 404 if the appraisal does not exist or is not a migrated appraisal book (HasAppraisalBook = false).")
            .WithTags("Asset Summary");
    }
}
