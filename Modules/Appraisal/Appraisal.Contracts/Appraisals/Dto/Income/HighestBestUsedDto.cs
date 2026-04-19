namespace Appraisal.Contracts.Appraisals.Dto.Income;

/// <summary>
/// Highest-and-best-used land top-up inputs (user-entered).
/// Derived totals (TotalWa, TotalValue) are recomputed client-side.
/// </summary>
public record HighestBestUsedDto(
    int? AreaRai,
    int? AreaNgan,
    decimal? AreaWa,
    decimal? PricePerSqWa
);
