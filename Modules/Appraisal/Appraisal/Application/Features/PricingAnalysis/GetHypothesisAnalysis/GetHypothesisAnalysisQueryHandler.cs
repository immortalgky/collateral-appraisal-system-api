using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetHypothesisAnalysis;

public class GetHypothesisAnalysisQueryHandler(
    IPricingAnalysisRepository repository
) : IQueryHandler<GetHypothesisAnalysisQuery, GetHypothesisAnalysisResult>
{
    public async Task<GetHypothesisAnalysisResult> Handle(
        GetHypothesisAnalysisQuery query,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(query.PricingAnalysisId, cancellationToken)
                              ?? throw new InvalidOperationException(
                                  $"PricingAnalysis {query.PricingAnalysisId} not found");

        var method = pricingAnalysis.Approaches
                         .SelectMany(a => a.Methods)
                         .FirstOrDefault(m => m.Id == query.MethodId)
                     ?? throw new InvalidOperationException($"Method {query.MethodId} not found");

        if (method.HypothesisAnalysis is null)
        {
            return new GetHypothesisAnalysisResult(
                null, null, null, null,
                [], [], [], [], method.Remark);
        }

        var ha = method.HypothesisAnalysis;

        var uploads = ha.Uploads
            .OrderByDescending(u => u.UploadedAt)
            .Select(u => new UploadHistoryDto(u.Id, u.FileName, u.UploadedAt, u.IsActive, u.RowCount))
            .ToList();

        var lbRows = ha.LandBuildingUnitRows
            .OrderBy(r => r.SequenceNumber)
            .Select(r => new LandBuildingUnitRowDto(
                r.SequenceNumber, r.PlanNo, r.HouseNo, r.ModelName, r.LandAreaSqWa, r.SellingPrice))
            .ToList();

        var condoRows = ha.CondominiumUnitRows
            .OrderBy(r => r.SequenceNumber)
            .Select(r => new CondominiumUnitRowDto(
                r.SequenceNumber, r.FloorNo, r.Building, r.AptNo, r.ModelType, r.UsableAreaSqM, r.SellingPrice))
            .ToList();

        var costItems = ha.CostItems
            .OrderBy(i => i.Category)
            .ThenBy(i => i.DisplaySequence)
            .Select(i => new CostItemDto(
                i.Id, i.Category, i.Kind, i.Description, i.DisplaySequence,
                i.Amount, i.RateAmount, i.Quantity, i.RatePercent, i.CategoryRatio, i.ModelName))
            .ToList();

        return new GetHypothesisAnalysisResult(
            ha.Id,
            ha.Variant,
            ha.LandBuildingSummary,
            ha.CondominiumSummary,
            uploads,
            lbRows,
            condoRows,
            costItems,
            method.Remark);
    }
}
