using Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;

namespace Appraisal.Application.Features.PricingAnalysis.PreviewIncomeAnalysis;

/// <summary>
/// Request body for POST income-analysis:preview.
/// Identical shape to <see cref="SaveIncomeAnalysisRequest"/> — reuses the same input DTOs.
/// </summary>
public record PreviewIncomeAnalysisRequest(
    string TemplateCode,
    string TemplateName,
    int TotalNumberOfYears,
    int TotalNumberOfDayInYear,
    decimal CapitalizeRate,
    decimal DiscountedRate,
    IReadOnlyList<IncomeSectionInput> Sections,
    /// <summary>
    /// User-supplied override for the rounded final value.
    /// When non-null and > 0, the backend uses this value instead of the server-computed rounding.
    /// Null or 0 means "no override — recompute". Backward-compatible: existing callers that omit
    /// this field will receive the same server-computed FinalValueRounded as before.
    /// </summary>
    decimal? FinalValueRounded = null
);
