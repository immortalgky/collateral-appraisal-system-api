using Appraisal.Infrastructure;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAssetSummary;

public class GetAssetSummaryQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetAssetSummaryQuery, GetAssetSummaryResult>
{
    public async Task<GetAssetSummaryResult> Handle(
        GetAssetSummaryQuery query,
        CancellationToken cancellationToken)
    {
        // Guard: return 404 if the appraisal does not exist
        var appraisalExists = await dbContext.Appraisals
            .AnyAsync(a => a.Id == query.AppraisalId, cancellationToken);

        if (!appraisalExists)
            return new GetAssetSummaryResult(null, null);

        // Load both tables independently — no FK exists between them;
        // the join key (GroupSet) is intentionally left to the consumer.
        var items = await dbContext.AssetSummaries
            .Where(a => a.AppraisalId == query.AppraisalId)
            .OrderBy(a => a.GroupSet)
            .Select(a => new AssetSummaryItemDto(
                a.Id,
                a.PropertyType,
                a.AssetDetail,
                a.Area,
                a.PricePerUnit,
                a.EstimatedPrice,
                a.CurrentPrice,
                a.GroupSet,
                a.IsPricesCurrent))
            .ToListAsync(cancellationToken);

        var groups = await dbContext.AssetSummaryGroups
            .Where(g => g.AppraisalId == query.AppraisalId)
            .OrderBy(g => g.GroupSet)
            .Select(g => new AssetSummaryGroupDto(
                g.Id,
                g.GroupSet,
                g.AssetGroupDetail,
                g.SumEstimatedPrice,
                g.RoundEstimatedPrice,
                g.SumCurrentPrice,
                g.RoundCurrentPrice))
            .ToListAsync(cancellationToken);

        // Guard: check appraisal book has legacy data on asset summary or not. If not means this appraisal book is on going not migrate one, return null.
        if (items.Count == 0 && groups.Count == 0)
            return new GetAssetSummaryResult(null, null);

        return new GetAssetSummaryResult(items, groups);
    }
}
