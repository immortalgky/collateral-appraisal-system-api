using Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;

namespace Appraisal.Application.Features.PricingAnalysis.PreviewIncomeAnalysis;

// NOT ITransactionalCommand — preview never opens a transaction or calls SaveChangesAsync.
public record PreviewIncomeAnalysisCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    Guid AppraisalId,
    Guid PropertyId,
    string TemplateCode,
    string TemplateName,
    int TotalNumberOfYears,
    int TotalNumberOfDayInYear,
    decimal CapitalizeRate,
    decimal DiscountedRate,
    IReadOnlyList<IncomeSectionInput> Sections,
    decimal? FinalValueAdjust = null,
    bool IsHighestBestUsed = true,
    HighestBestUsedInput? HighestBestUsed = null,
    decimal? AppraisalPriceRounded = null
) : ICommand<PreviewIncomeAnalysisResult>;
