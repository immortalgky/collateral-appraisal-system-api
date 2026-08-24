using Appraisal.Application.Features.PricingAnalysis.GetPricingAnalysisDocuments;
using Appraisal.Application.Services;

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

        // Building value depends only on the analysis's anchor properties, not on any individual
        // method — computed once and applied to every method DTO (cheap; the frontend already
        // knows which method types to render it against).
        decimal buildingValue = 0m;
        if (pricingAnalysis.SubjectType == PricingAnalysisSubjectType.PropertyGroup
            && pricingAnalysis.AnchorId.HasValue)
            buildingValue = await propertyDataService.GetTotalBuildingValueAsync(
                pricingAnalysis.AnchorId.Value, cancellationToken);

        var approaches = pricingAnalysis.Approaches.Select(a => new ApproachDto(
            a.Id,
            a.ApproachType,
            a.IsSelected,
            a.Methods.Select(m => new MethodDto(
                m.Id,
                m.MethodType,
                m.MethodValue,
                m.IsSelected,
                m.UseSystemCalc,
                m.ComparativeAnalysisTemplateId,
                m.FinalValue?.LandValue,
                buildingValue
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
            pricingAnalysis.Remark
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
    bool UseSystemCalc,
    Guid? ComparativeAnalysisTemplateId,
    decimal? LandValue,
    decimal BuildingValue
);