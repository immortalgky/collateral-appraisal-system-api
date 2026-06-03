using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.GetHypothesisAnalysis;

public class GetHypothesisAnalysisQueryHandler(
    IPricingAnalysisRepository repository,
    PricingPropertyDataService propertyDataService
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

        // Fetch system land area from titles (PropertyGroup only)
        decimal? totalLandAreaFromTitles = null;
        if (pricingAnalysis.SubjectType == PricingAnalysisSubjectType.PropertyGroup
            && pricingAnalysis.AnchorId.HasValue)
            totalLandAreaFromTitles = await propertyDataService.GetTotalLandAreaFromTitlesAsync(
                pricingAnalysis.AnchorId.Value, cancellationToken);

        if (method.HypothesisAnalysis is null)
        {
            return new GetHypothesisAnalysisResult(
                null, null, null, null,
                [], [], [], [], method.Remark, totalLandAreaFromTitles);
        }

        var ha = method.HypothesisAnalysis;

        var uploads = ha.Uploads
            .OrderByDescending(u => u.UploadedAt)
            .Select(u => new UploadHistoryDto(u.Id, u.FileName, u.UploadedAt, u.IsActive, u.RowCount))
            .ToList();

        var lbRows = ha.LandBuildingUnitRows
            .OrderBy(r => r.SequenceNumber)
            .Select(r => new LandBuildingUnitRowDto(
                r.SequenceNumber, r.PlanNo, r.HouseNo, r.ModelName, r.Location, r.FloorNo,
                r.LandAreaSqWa, r.UsableAreaSqM, r.SellingPrice, r.Remark1, r.Remark2))
            .ToList();

        var condoRows = ha.CondominiumUnitRows
            .OrderBy(r => r.SequenceNumber)
            .Select(r => new CondominiumUnitRowDto(
                r.SequenceNumber, r.FloorNo, r.Building, r.AptNo, r.Apartment, r.ModelType,
                r.UsableAreaSqM, r.SellingPrice, r.Remark1, r.Remark2))
            .ToList();

        var costItems = ha.CostItems
            .OrderBy(i => i.Category)
            .ThenBy(i => i.DisplaySequence)
            .Select(i => new CostItemDto(
                i.Id, i.Category, i.Kind, i.Description, i.DisplaySequence,
                i.Amount, i.RateAmount, i.Quantity, i.RatePercent, i.CategoryRatio, i.ModelName,
                i.IsBuilding, i.DepreciationMethod,
                i.Area, i.PricePerSqM, i.PriceBeforeDepreciation,
                i.Year, i.AnnualDepreciationPercent,
                i.TotalDepreciationPercent, i.DepreciationAmount, i.ValueAfterDepreciation,
                i.DepreciationPeriods
                    .OrderBy(p => p.Sequence)
                    .Select(p => new DepreciationPeriodDto(p.Id, p.Sequence, p.AtYear, p.ToYear, p.DepreciationPerYear))
                    .ToList()))
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
            method.Remark,
            totalLandAreaFromTitles);
    }
}
