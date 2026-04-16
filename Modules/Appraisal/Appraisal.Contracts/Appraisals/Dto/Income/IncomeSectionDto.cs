namespace Appraisal.Contracts.Appraisals.Dto.Income;

/// <summary>
/// A top-level section in IncomeAnalysis (e.g. "Income", "Expenses", "DCF Summary").
/// </summary>
public record IncomeSectionDto(
    Guid Id,
    string SectionType,
    string SectionName,
    string Identifier,
    int DisplaySeq,
    decimal[] TotalSectionValues,
    IReadOnlyList<IncomeCategoryDto> Categories
);
