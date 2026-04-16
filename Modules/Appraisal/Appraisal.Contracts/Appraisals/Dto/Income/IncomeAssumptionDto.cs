namespace Appraisal.Contracts.Appraisals.Dto.Income;

/// <summary>
/// A single assumption line within an IncomeCategory, with its calculation method and computed totals.
/// </summary>
public record IncomeAssumptionDto(
    Guid Id,
    string AssumptionType,
    string AssumptionName,
    string Identifier,
    int DisplaySeq,
    decimal[] TotalAssumptionValues,
    IncomeMethodDto Method
);
