using System.Text.Json;

namespace Appraisal.Application.Features.PricingAnalysis.SaveIncomeAnalysis;

/// <summary>
/// Full-replace request body for a PUT income-analysis save.
/// The client submits the entire tree; the server recalculates and returns canonical values.
/// </summary>
public record SaveIncomeAnalysisRequest(
    string TemplateCode,
    string TemplateName,
    int TotalNumberOfYears,
    int TotalNumberOfDayInYear,
    decimal CapitalizeRate,
    decimal DiscountedRate,
    IReadOnlyList<IncomeSectionInput> Sections
);

public record IncomeSectionInput(
    string SectionType,
    string SectionName,
    string Identifier,
    int DisplaySeq,
    IReadOnlyList<IncomeCategoryInput> Categories
);

public record IncomeCategoryInput(
    string CategoryType,
    string CategoryName,
    string Identifier,
    int DisplaySeq,
    IReadOnlyList<IncomeAssumptionInput> Assumptions
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
    JsonElement Detail
);
