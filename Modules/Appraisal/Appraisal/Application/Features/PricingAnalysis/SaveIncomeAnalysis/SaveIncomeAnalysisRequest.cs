using System.Text.Json;

namespace Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;

/// <summary>
/// Full-replace request body for a PUT income-analysis save.
/// The client submits the entire tree; the server recalculates and returns canonical values.
/// </summary>
public record SaveIncomeAnalysisRequest(
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

public record HighestBestUsedInput(
    int? AreaRai,
    int? AreaNgan,
    decimal? AreaWa,
    decimal? PricePerSqWa
);

public record IncomeSectionInput(
    string SectionType,
    string SectionName,
    string Identifier,
    int DisplaySeq,
    IReadOnlyList<IncomeCategoryInput> Categories,
    /// <summary>
    /// Client-assigned transient ID for this section (Guid-formatted string).
    /// Used to resolve Method-13 refTarget.clientId references on first save.
    /// Optional: omit or pass null for re-saves where dbId is already populated.
    /// </summary>
    string? ClientId = null
);

public record IncomeCategoryInput(
    string CategoryType,
    string CategoryName,
    string Identifier,
    int DisplaySeq,
    IReadOnlyList<IncomeAssumptionInput> Assumptions,
    /// <summary>
    /// Client-assigned transient ID for this category (Guid-formatted string).
    /// Used to resolve Method-13 refTarget.clientId references on first save.
    /// Optional: omit or pass null for re-saves where dbId is already populated.
    /// </summary>
    string? ClientId = null
);

public record IncomeAssumptionInput(
    string AssumptionType,
    string AssumptionName,
    string Identifier,
    int DisplaySeq,
    string MethodTypeCode,
    /// <summary>
    /// Raw method parameters as sent by the client (one of the 14 detail shapes).
    /// Validated server-side via MethodDetailSerializer before persistence.
    /// </summary>
    JsonElement Detail,
    /// <summary>
    /// Client-assigned transient ID for this assumption (Guid-formatted string).
    /// Used to resolve Method-13 refTarget.clientId references on first save.
    /// Optional: omit or pass null for re-saves where dbId is already populated.
    /// </summary>
    string? ClientId = null
);
