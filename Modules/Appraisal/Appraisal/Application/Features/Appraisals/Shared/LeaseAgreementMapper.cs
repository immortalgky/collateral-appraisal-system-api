using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.Appraisals.Shared;

/// <summary>
/// Maps domain entities to shared response DTOs for lease agreement properties.
/// </summary>
public static class LeaseAgreementMapper
{
    public static LeaseAgreementDetailDto? MapLeaseAgreement(LeaseAgreementDetail? detail)
    {
        if (detail is null) return null;

        return new LeaseAgreementDetailDto(
            detail.Id,
            detail.AppraisalPropertyId,
            detail.LesseeName,
            detail.LessorName,
            detail.LeasePeriodAsContract,
            detail.RemainingLeaseAsAppraisalDate,
            detail.ContractNo,
            detail.LeaseStartDate,
            detail.LeaseEndDate,
            detail.LeaseRentFee,
            detail.RentAdjust,
            detail.Sublease,
            detail.AdditionalExpenses,
            detail.LeaseTerminate,
            detail.ContractRenewal,
            detail.RentalTermsImpactingPropertyUse,
            detail.TerminationOfLease,
            detail.Remark
        );
    }

    public static RentalInfoDto? MapRentalInfo(RentalInfo? info)
    {
        if (info is null) return null;

        return new RentalInfoDto(
            info.Id,
            info.AppraisalPropertyId,
            info.NumberOfYears,
            info.FirstYearStartDate,
            info.ContractRentalFeePerYear,
            info.UpFrontTotalAmount,
            info.GrowthRateType,
            info.GrowthRatePercent,
            info.GrowthIntervalYears,
            info.UpFrontEntries
                .OrderBy(e => e.AtYear)
                .Select(e => new UpFrontEntryResponseDto(e.Id, e.AtYear, e.UpFrontAmount))
                .ToList(),
            info.GrowthPeriodEntries
                .OrderBy(e => e.FromYear)
                .Select(e => new GrowthPeriodEntryResponseDto(
                    e.Id, e.FromYear, e.ToYear, e.GrowthRate, e.GrowthAmount, e.TotalAmount))
                .ToList(),
            info.ScheduleEntries
                .OrderBy(e => e.Year)
                .Select(e => new ScheduleEntryResponseDto(
                    e.Year, e.ContractStart, e.ContractEnd, e.UpFront,
                    e.ContractRentalFee, e.TotalAmount, e.ContractRentalFeeGrowthRatePercent))
                .ToList(),
            info.ScheduleOverrides
                .OrderBy(e => e.Year)
                .Select(e => new ScheduleOverrideResponseDto(e.Year, e.UpFront, e.ContractRentalFee))
                .ToList()
        );
    }
}
