namespace Appraisal.Contracts.Appraisals.Dto.Income;

/// <summary>
/// Full income-analysis tree returned to the client after save or get.
/// All computed arrays (totals, summary) are server-canonical values.
/// </summary>
public record IncomeAnalysisDto(
    Guid Id,
    Guid PricingAnalysisMethodId,
    string TemplateCode,
    string TemplateName,
    int TotalNumberOfYears,
    int TotalNumberOfDayInYear,
    decimal CapitalizeRate,
    decimal DiscountedRate,
    decimal? FinalValue,
    decimal? FinalValueRounded,
    IReadOnlyList<IncomeSectionDto> Sections,
    IncomeSummaryDto Summary
);
