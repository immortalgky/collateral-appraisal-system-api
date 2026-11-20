namespace Appraisal.Application.Features.Appraisals.Shared;

/// <summary>
/// Lease agreement &amp; rental info of a plain property rented out to others (today the condo; land keeps its own
/// copy of this block): rented out creates the two owned rows and applies what was sent, anything else clears
/// them so the owned rows are deleted.
/// </summary>
internal static class RentedOutSync
{
    public static void Apply(
        AppraisalProperty property, bool? isRentedOut, LeaseAgreementData? lease, RentalInfoData? rental)
    {
        if (isRentedOut != true)
        {
            property.ClearLeaseAgreementDetail();
            property.ClearRentalInfo();
            return;
        }

        if (property.LeaseAgreementDetail is null)
            property.SetLeaseAgreementDetail(LeaseAgreementDetail.Create(property.Id));
        if (property.RentalInfo is null)
            property.SetRentalInfo(RentalInfo.Create(property.Id));

        if (lease is not null)
            property.LeaseAgreementDetail!.Update(
                lease.LesseeName, lease.LessorName,
                lease.LeasePeriodAsContract, lease.RemainingLeaseAsAppraisalDate,
                lease.ContractNo, lease.LeaseStartDate, lease.LeaseEndDate,
                lease.LeaseRentFee, lease.RentAdjust,
                lease.Sublease, lease.AdditionalExpenses,
                lease.LeaseTerminate, lease.ContractRenewal,
                lease.RentalTermsImpactingPropertyUse, lease.TerminationOfLease,
                lease.Remark,
                lease.IsLeaseRegistered, lease.IsLeaseholdPledgeable);

        if (rental is null)
            return;

        var info = property.RentalInfo!;
        info.Update(
            rental.NumberOfYears, rental.FirstYearStartDate,
            rental.ContractRentalFeePerYear, rental.UpFrontTotalAmount,
            rental.GrowthRateType, rental.GrowthRatePercent,
            rental.GrowthIntervalYears);

        if (rental.UpFrontEntries is not null)
        {
            info.ClearUpFrontEntries();
            foreach (var entry in rental.UpFrontEntries)
                info.AddUpFrontEntry(entry.AtYear, entry.UpFrontAmount);
        }

        if (rental.GrowthPeriodEntries is not null)
        {
            info.ClearGrowthPeriodEntries();
            foreach (var entry in rental.GrowthPeriodEntries)
                info.AddGrowthPeriodEntry(entry.FromYear, entry.ToYear, entry.GrowthRate, entry.GrowthAmount, entry.TotalAmount);
        }

        RentalScheduleComputer.ComputeAndSave(info, rental.ScheduleOverrides);
    }
}
