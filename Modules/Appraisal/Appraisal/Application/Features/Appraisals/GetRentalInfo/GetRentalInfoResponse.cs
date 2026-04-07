namespace Appraisal.Application.Features.Appraisals.GetRentalInfo;

public record GetRentalInfoResponse(
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
