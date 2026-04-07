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

public record UpFrontEntryData(DateTime AtYear, decimal UpFrontAmount);
public record GrowthPeriodEntryData(int FromYear, int ToYear, decimal GrowthRate, decimal GrowthAmount, decimal TotalAmount);
public record RentalScheduleEntryData(int Year, DateTime ContractStart, DateTime ContractEnd,
    decimal UpFront, decimal ContractRentalFee, decimal TotalAmount, decimal ContractRentalFeeGrowthRatePercent);
public record RentalScheduleOverrideData(int Year, decimal? UpFront, decimal? ContractRentalFee);

// ─── Response DTOs (used in GET lease agreement property results) ─────────────

public record LeaseAgreementDetailDto(
    Guid DetailId,
    Guid AppraisalPropertyId,
    string? LesseeName,
    string? TenantName,
    string? LeasePeriodAsContract,
    string? RemainingLeaseAsAppraisalDate,
    string? ContractNo,
    DateTime? LeaseStartDate,
    DateTime? LeaseEndDate,
    decimal? LeaseRentFee,
    decimal? RentAdjust,
    string? Sublease,
    string? AdditionalExpenses,
    string? LeaseTimestamp,
    string? ContractRenewal,
    string? RentalTermsImpactingPropertyUse,
    string? TerminationOfLease,
    string? Remark,
    string? Banking
);

public record RentalInfoDto(
    Guid DetailId,
    Guid AppraisalPropertyId,
    int NumberOfYears,
    DateTime? FirstYearStartDate,
    decimal ContractRentalFeePerYear,
    decimal UpFrontTotalAmount,
    string? GrowthRateType,
    decimal GrowthRatePercent,
    int GrowthIntervalYears,
    List<UpFrontEntryResponseDto> UpFrontEntries,
    List<GrowthPeriodEntryResponseDto> GrowthPeriodEntries,
    List<ScheduleEntryResponseDto> ScheduleEntries,
    List<ScheduleOverrideResponseDto> ScheduleOverrides
);

public record UpFrontEntryResponseDto(Guid Id, DateTime AtYear, decimal UpFrontAmount);
public record GrowthPeriodEntryResponseDto(Guid Id, int FromYear, int ToYear, decimal GrowthRate, decimal GrowthAmount, decimal TotalAmount);
public record ScheduleEntryResponseDto(int Year, DateTime ContractStart, DateTime ContractEnd, decimal UpFront, decimal ContractRentalFee, decimal TotalAmount, decimal ContractRentalFeeGrowthRatePercent);
public record ScheduleOverrideResponseDto(int Year, decimal? UpFront, decimal? ContractRentalFee);
