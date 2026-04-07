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
    List<GrowthPeriodEntryDto> GrowthPeriodEntries,
    List<ScheduleEntryDto> ScheduleEntries,
    List<ScheduleOverrideDto> ScheduleOverrides
);

public record UpFrontEntryDto(Guid Id, DateTime AtYear, decimal UpFrontAmount);
public record GrowthPeriodEntryDto(Guid Id, int FromYear, int ToYear, decimal GrowthRate, decimal GrowthAmount, decimal TotalAmount);
public record ScheduleEntryDto(int Year, DateTime ContractStart, DateTime ContractEnd, decimal UpFront, decimal ContractRentalFee, decimal TotalAmount, decimal ContractRentalFeeGrowthRatePercent);
public record ScheduleOverrideDto(int Year, decimal? UpFront, decimal? ContractRentalFee);
