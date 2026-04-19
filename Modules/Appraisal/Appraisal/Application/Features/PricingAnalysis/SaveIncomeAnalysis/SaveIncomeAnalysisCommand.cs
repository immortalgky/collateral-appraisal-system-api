using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;

public record SaveIncomeAnalysisCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
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
) : ICommand<SaveIncomeAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
