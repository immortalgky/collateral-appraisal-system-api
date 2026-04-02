namespace Appraisal.Application.Features.Appraisals.GetRentalInfo;

public record GetRentalInfoResult(
    Guid DetailId,
    Guid AppraisalPropertyId,
    int NumberOfYears,
    DateTime? FirstYearStartDate,
    decimal ContractRentalFeePerYear,
    decimal UpFrontTotalAmount,
    string? GrowthRateType,
    decimal GrowthRatePercent,
    int GrowthIntervalYears,
    List<UpFrontEntryDto> UpFrontEntries,
    List<GrowthPeriodEntryDto> GrowthPeriodEntries
);

public record UpFrontEntryDto(Guid Id, int AtYear, decimal UpFrontAmount);
public record GrowthPeriodEntryDto(Guid Id, int FromYear, int ToYear, decimal GrowthRate, decimal GrowthAmount, decimal TotalAmount);
