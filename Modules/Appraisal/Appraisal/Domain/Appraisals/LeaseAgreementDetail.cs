namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Lease agreement contract details for a property.
/// 1:1 relationship with AppraisalProperty (PropertyType = LSL, LSB, or LS)
/// </summary>
public class LeaseAgreementDetail : Entity<Guid>
{
    public Guid AppraisalPropertyId { get; private set; }

    // Information
    public string? LesseeName { get; private set; }
    public string? LessorName { get; private set; }

    // Contract
    public decimal? LeasePeriodAsContract { get; private set; }
    public decimal? RemainingLeaseAsAppraisalDate { get; private set; }
    public string? ContractNo { get; private set; }

    // Dates & Fees
    public DateTime? LeaseStartDate { get; private set; }
    public DateTime? LeaseEndDate { get; private set; }
    public decimal? LeaseRentFee { get; private set; }
    public decimal? RentAdjust { get; private set; }

    // Terms
    public string? Sublease { get; private set; }
    public decimal? AdditionalExpenses { get; private set; }
    public string? LeaseTerminate { get; private set; }
    public string? ContractRenewal { get; private set; }

    // Long text
    public string? RentalTermsImpactingPropertyUse { get; private set; }
    public string? TerminationOfLease { get; private set; }

    // Other
    public string? Remark { get; private set; }

    private LeaseAgreementDetail()
    {
    }

    public static LeaseAgreementDetail Create(Guid appraisalPropertyId)
    {
        return new LeaseAgreementDetail
        {
            AppraisalPropertyId = appraisalPropertyId,
        };
    }

    public static LeaseAgreementDetail CopyFrom(LeaseAgreementDetail source, Guid newPropertyId)
    {
        return new LeaseAgreementDetail
        {
            AppraisalPropertyId = newPropertyId,
            LesseeName = source.LesseeName,
            LessorName = source.LessorName,
            LeasePeriodAsContract = source.LeasePeriodAsContract,
            RemainingLeaseAsAppraisalDate = source.RemainingLeaseAsAppraisalDate,
            ContractNo = source.ContractNo,
            LeaseStartDate = source.LeaseStartDate,
            LeaseEndDate = source.LeaseEndDate,
            LeaseRentFee = source.LeaseRentFee,
            RentAdjust = source.RentAdjust,
            Sublease = source.Sublease,
            AdditionalExpenses = source.AdditionalExpenses,
            LeaseTerminate = source.LeaseTerminate,
            ContractRenewal = source.ContractRenewal,
            RentalTermsImpactingPropertyUse = source.RentalTermsImpactingPropertyUse,
            TerminationOfLease = source.TerminationOfLease,
            Remark = source.Remark,
        };
    }

    public void Update(
        string? lesseeName = null,
        string? lessorName = null,
        decimal? leasePeriodAsContract = null,
        decimal? remainingLeaseAsAppraisalDate = null,
        string? contractNo = null,
        DateTime? leaseStartDate = null,
        DateTime? leaseEndDate = null,
        decimal? leaseRentFee = null,
        decimal? rentAdjust = null,
        string? sublease = null,
        decimal? additionalExpenses = null,
        string? leaseTerminate = null,
        string? contractRenewal = null,
        string? rentalTermsImpactingPropertyUse = null,
        string? terminationOfLease = null,
        string? remark = null)
    {
        if (lesseeName is not null) LesseeName = lesseeName;
        if (lessorName is not null) LessorName = lessorName;
        if (leasePeriodAsContract is not null) LeasePeriodAsContract = leasePeriodAsContract;
        if (remainingLeaseAsAppraisalDate is not null) RemainingLeaseAsAppraisalDate = remainingLeaseAsAppraisalDate;
        if (contractNo is not null) ContractNo = contractNo;
        if (leaseStartDate.HasValue) LeaseStartDate = leaseStartDate.Value;
        if (leaseEndDate.HasValue) LeaseEndDate = leaseEndDate.Value;
        if (leaseRentFee.HasValue) LeaseRentFee = leaseRentFee.Value;
        if (rentAdjust.HasValue) RentAdjust = rentAdjust.Value;
        if (sublease is not null) Sublease = sublease;
        if (additionalExpenses is not null) AdditionalExpenses = additionalExpenses;
        if (leaseTerminate is not null) LeaseTerminate = leaseTerminate;
        if (contractRenewal is not null) ContractRenewal = contractRenewal;
        if (rentalTermsImpactingPropertyUse is not null) RentalTermsImpactingPropertyUse = rentalTermsImpactingPropertyUse;
        if (terminationOfLease is not null) TerminationOfLease = terminationOfLease;
        if (remark is not null) Remark = remark;
    }

    /// <summary>
    /// Applies admin corrections to this lease agreement, recording each change in <paramref name="diff"/>.
    /// </summary>
    internal void ApplyCorrection(LeaseAgreementCorrection edit, Dictionary<string, object?> diff)
    {
        CorrectionDiff.Apply("Lease.LesseeName", LesseeName, edit.LesseeName, v => LesseeName = v, diff);
        CorrectionDiff.Apply("Lease.LessorName", LessorName, edit.LessorName, v => LessorName = v, diff);
        CorrectionDiff.Apply("Lease.LeasePeriodAsContract", LeasePeriodAsContract, edit.LeasePeriodAsContract, v => LeasePeriodAsContract = v, diff);
        CorrectionDiff.Apply("Lease.RemainingLeaseAsAppraisalDate", RemainingLeaseAsAppraisalDate, edit.RemainingLeaseAsAppraisalDate, v => RemainingLeaseAsAppraisalDate = v, diff);
        CorrectionDiff.Apply("Lease.ContractNo", ContractNo, edit.ContractNo, v => ContractNo = v, diff);
        CorrectionDiff.Apply("Lease.LeaseStartDate", LeaseStartDate, edit.LeaseStartDate, v => LeaseStartDate = v, diff);
        CorrectionDiff.Apply("Lease.LeaseEndDate", LeaseEndDate, edit.LeaseEndDate, v => LeaseEndDate = v, diff);
        CorrectionDiff.Apply("Lease.LeaseRentFee", LeaseRentFee, edit.LeaseRentFee, v => LeaseRentFee = v, diff);
        CorrectionDiff.Apply("Lease.RentAdjust", RentAdjust, edit.RentAdjust, v => RentAdjust = v, diff);
        CorrectionDiff.Apply("Lease.Sublease", Sublease, edit.Sublease, v => Sublease = v, diff);
        CorrectionDiff.Apply("Lease.AdditionalExpenses", AdditionalExpenses, edit.AdditionalExpenses, v => AdditionalExpenses = v, diff);
        CorrectionDiff.Apply("Lease.LeaseTerminate", LeaseTerminate, edit.LeaseTerminate, v => LeaseTerminate = v, diff);
        CorrectionDiff.Apply("Lease.ContractRenewal", ContractRenewal, edit.ContractRenewal, v => ContractRenewal = v, diff);
        CorrectionDiff.Apply("Lease.RentalTermsImpactingPropertyUse", RentalTermsImpactingPropertyUse, edit.RentalTermsImpactingPropertyUse, v => RentalTermsImpactingPropertyUse = v, diff);
        CorrectionDiff.Apply("Lease.TerminationOfLease", TerminationOfLease, edit.TerminationOfLease, v => TerminationOfLease = v, diff);
        CorrectionDiff.Apply("Lease.Remark", Remark, edit.Remark, v => Remark = v, diff);
    }
}
