namespace Appraisal.Application.Features.Appraisals.Shared;

/// <summary>
/// Shared data for lease agreement detail — used in Create and Update commands.
/// </summary>
public record LeaseAgreementData(
    string? LesseeName = null,
    string? TenantName = null,
    string? LeasePeriodAsContract = null,
    string? RemainingLeaseAsAppraisalDate = null,
    string? ContractNo = null,
    DateTime? LeaseStartDate = null,
    DateTime? LeaseEndDate = null,
    decimal? LeaseRentFee = null,
    decimal? RentAdjust = null,
    string? Sublease = null,
    string? AdditionalExpenses = null,
    string? LeaseTimestamp = null,
    string? ContractRenewal = null,
    string? RentalTermsImpactingPropertyUse = null,
    string? TerminationOfLease = null,
    string? Remark = null,
    string? Banking = null
);

/// <summary>
/// Shared data for rental info — used in Create and Update commands.
/// </summary>
public record RentalInfoData(
    int? NumberOfYears = null,
    DateTime? FirstYearStartDate = null,
    decimal? ContractRentalFeePerYear = null,
    decimal? UpFrontTotalAmount = null,
    string? GrowthRateType = null,
    decimal? GrowthRatePercent = null,
    int? GrowthIntervalYears = null,
    List<UpFrontEntryData>? UpFrontEntries = null,
    List<GrowthPeriodEntryData>? GrowthPeriodEntries = null,
    List<RentalScheduleEntryData>? ScheduleEntries = null,
    List<RentalScheduleOverrideData>? ScheduleOverrides = null
);

public record UpFrontEntryData(int AtYear, decimal UpFrontAmount);
public record GrowthPeriodEntryData(int FromYear, int ToYear, decimal GrowthRate, decimal GrowthAmount, decimal TotalAmount);
public record RentalScheduleEntryData(int Year, DateTime ContractStart, DateTime ContractEnd,
    decimal UpFront, decimal ContractRentalFee, decimal TotalAmount, decimal ContractRentalFeeGrowthRatePercent);
public record RentalScheduleOverrideData(int Year, decimal? UpFront, decimal? ContractRentalFee);
