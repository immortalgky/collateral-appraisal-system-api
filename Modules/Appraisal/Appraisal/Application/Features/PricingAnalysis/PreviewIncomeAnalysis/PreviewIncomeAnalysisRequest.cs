using Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;

namespace Appraisal.Application.Features.PricingAnalysis.PreviewIncomeAnalysis;

/// <summary>
/// Request body for POST income-analysis:preview.
/// Identical shape to <see cref="SaveIncomeAnalysisRequest"/> — reuses the same input DTOs.
/// </summary>
public record PreviewIncomeAnalysisRequest(
    Guid AppraisalId,
    Guid PropertyId,
    string TemplateCode,
    string TemplateName,
    int TotalNumberOfYears,
    int TotalNumberOfDayInYear,
    decimal CapitalizeRate,
    decimal DiscountedRate,
    IReadOnlyList<IncomeSectionInput> Sections,
    /// <summary>
    /// User-adjustable final value. Defaults to FinalValueRounded on the frontend.
    /// Stored as-is; the backend never recomputes it.
    /// </summary>
    decimal? FinalValueAdjust = null,
    bool IsHighestBestUsed = true,
    HighestBestUsedInput? HighestBestUsed = null,
    decimal? AppraisalPriceRounded = null
);
