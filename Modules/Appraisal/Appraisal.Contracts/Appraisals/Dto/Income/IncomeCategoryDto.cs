namespace Appraisal.Contracts.Appraisals.Dto.Income;

/// <summary>
/// A category within an IncomeSection (e.g. "Room Revenue", "Fixed Expenses").
/// </summary>
public record IncomeCategoryDto(
    Guid Id,
    string CategoryType,
    string CategoryName,
    string Identifier,
    int DisplaySeq,
    decimal[] TotalCategoryValues,
    IReadOnlyList<IncomeAssumptionDto> Assumptions
);
