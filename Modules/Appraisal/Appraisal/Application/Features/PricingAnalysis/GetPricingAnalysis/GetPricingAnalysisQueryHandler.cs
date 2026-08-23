using Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysisDocuments;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysis;

/// <summary>
/// Handler for getting a pricing analysis by ID
/// </summary>
public class GetPricingAnalysisQueryHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    PricingPropertyDataService propertyDataService
) : IQueryHandler<GetPricingAnalysisQuery, GetPricingAnalysisResult>
{
    public async Task<GetPricingAnalysisResult> Handle(
        GetPricingAnalysisQuery query,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
                                  query.Id,
                                  cancellationToken)
                              ?? throw new InvalidOperationException($"Pricing analysis {query.Id} not found");

        var approaches = pricingAnalysis.Approaches.Select(a => new ApproachDto(
            a.Id,
            a.ApproachType,
            a.IsSelected,
            a.Methods.Select(m => new MethodDto(
                m.Id,
                m.MethodType,
                m.MethodValue,
                m.IsSelected,
                m.ComparativeAnalysisTemplateId,
                m.ValuePerUnit,
                m.UnitType,
                m.FinalValue?.LandArea,
                m.FinalValue?.LandValue,
                m.FinalValue?.BuildingValue,
                m.FinalValue?.AppraisalPrice
            )).ToList()
        )).ToList();

        var documents = pricingAnalysis.Documents.Select(d => new PricingAnalysisDocumentDto(
            d.Id,
            d.DocumentId,
            d.FileName,
            d.FilePath,
            d.UploadedBy,
            d.UploadedByName,
            d.UploadedAt)).ToList();

        // Land area and the building schedule are group-scoped, so a reference sub-analysis has
        // neither. Both are what a manual Cost breakdown multiplies and adds, so the client reads
        // them from here instead of re-deriving them from title deeds itself.
        decimal? landAreaInSqWa = null;
        decimal? buildingValue = null;

        if (pricingAnalysis.SubjectType == PricingAnalysisSubjectType.PropertyGroup
            && pricingAnalysis.AnchorId.HasValue)
        {
            // The SQL lookup, not GetTotalLandAreaFromTitlesAsync — this endpoint is on the
            // pricing screen's load path for every property group, and the aggregate version
            // loads the whole Appraisal with all its properties to sum the same three terms.
            landAreaInSqWa = await propertyDataService.GetTotalLandAreaInSqWaAsync(
                pricingAnalysis.AnchorId.Value, cancellationToken);

            buildingValue = await propertyDataService.GetTotalBuildingCostAsync(
                pricingAnalysis.AnchorId.Value, cancellationToken);
        }

        return new GetPricingAnalysisResult(
            pricingAnalysis.Id,
            pricingAnalysis.SubjectType,
            pricingAnalysis.AnchorId,
            pricingAnalysis.AnchorRefKey,
            pricingAnalysis.HostMethodId,
            pricingAnalysis.Status,
            pricingAnalysis.FinalAppraisedValue,
            pricingAnalysis.UseSystemCalc,
            approaches,
            documents,
            pricingAnalysis.Remark,
            landAreaInSqWa,
            buildingValue
        );
    }
}

public record ApproachDto(
    Guid Id,
    string ApproachType,
    bool IsSelected,
    List<MethodDto> Methods
);

public record MethodDto(
    Guid Id,
    string MethodType,
    decimal? MethodValue,
    bool IsSelected,
    Guid? ComparativeAnalysisTemplateId,
    // Recorded breakdown, so a saved manual Cost entry reloads into the form it was typed in.
    decimal? ValuePerUnit = null,
    string? UnitType = null,
    decimal? LandArea = null,
    decimal? LandValue = null,
    decimal? BuildingValue = null,
    decimal? AppraisalPrice = null
);
