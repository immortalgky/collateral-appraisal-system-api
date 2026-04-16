using Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;

namespace Appraisal.Application.Features.PricingAnalysis.PreviewIncomeAnalysis;

// NOT ITransactionalCommand — preview never opens a transaction or calls SaveChangesAsync.
public record PreviewIncomeAnalysisCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    string TemplateCode,
    string TemplateName,
    int TotalNumberOfYears,
    int TotalNumberOfDayInYear,
    decimal CapitalizeRate,
    decimal DiscountedRate,
    IReadOnlyList<IncomeSectionInput> Sections,
    decimal? FinalValueRounded = null
) : ICommand<PreviewIncomeAnalysisResult>;
