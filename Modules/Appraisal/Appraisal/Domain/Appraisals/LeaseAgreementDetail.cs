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
    public string? TenantName { get; private set; }

    // Contract
    public string? LeasePeriodAsContract { get; private set; }
    public string? RemainingLeaseAsAppraisalDate { get; private set; }
    public string? ContractNo { get; private set; }

    // Dates & Fees
    public DateTime? LeaseStartDate { get; private set; }
    public DateTime? LeaseEndDate { get; private set; }
    public decimal? LeaseRentFee { get; private set; }
    public decimal? RentAdjust { get; private set; }

    // Terms
    public string? Sublease { get; private set; }
    public string? AdditionalExpenses { get; private set; }
    public string? LeaseTimestamp { get; private set; }
    public string? ContractRenewal { get; private set; }

    // Long text
    public string? RentalTermsImpactingPropertyUse { get; private set; }
    public string? TerminationOfLease { get; private set; }

    // Other
    public string? Remark { get; private set; }
    public string? Banking { get; private set; }

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
            TenantName = source.TenantName,
            LeasePeriodAsContract = source.LeasePeriodAsContract,
            RemainingLeaseAsAppraisalDate = source.RemainingLeaseAsAppraisalDate,
            ContractNo = source.ContractNo,
            LeaseStartDate = source.LeaseStartDate,
            LeaseEndDate = source.LeaseEndDate,
            LeaseRentFee = source.LeaseRentFee,
            RentAdjust = source.RentAdjust,
            Sublease = source.Sublease,
            AdditionalExpenses = source.AdditionalExpenses,
            LeaseTimestamp = source.LeaseTimestamp,
            ContractRenewal = source.ContractRenewal,
            RentalTermsImpactingPropertyUse = source.RentalTermsImpactingPropertyUse,
            TerminationOfLease = source.TerminationOfLease,
            Remark = source.Remark,
            Banking = source.Banking
        };
    }

    public void Update(
        string? lesseeName = null,
        string? tenantName = null,
        string? leasePeriodAsContract = null,
        string? remainingLeaseAsAppraisalDate = null,
        string? contractNo = null,
        DateTime? leaseStartDate = null,
        DateTime? leaseEndDate = null,
        decimal? leaseRentFee = null,
        decimal? rentAdjust = null,
        string? sublease = null,
        string? additionalExpenses = null,
        string? leaseTimestamp = null,
        string? contractRenewal = null,
        string? rentalTermsImpactingPropertyUse = null,
        string? terminationOfLease = null,
        string? remark = null,
        string? banking = null)
    {
        if (lesseeName is not null) LesseeName = lesseeName;
        if (tenantName is not null) TenantName = tenantName;
        if (leasePeriodAsContract is not null) LeasePeriodAsContract = leasePeriodAsContract;
        if (remainingLeaseAsAppraisalDate is not null) RemainingLeaseAsAppraisalDate = remainingLeaseAsAppraisalDate;
        if (contractNo is not null) ContractNo = contractNo;
        if (leaseStartDate.HasValue) LeaseStartDate = leaseStartDate.Value;
        if (leaseEndDate.HasValue) LeaseEndDate = leaseEndDate.Value;
        if (leaseRentFee.HasValue) LeaseRentFee = leaseRentFee.Value;
        if (rentAdjust.HasValue) RentAdjust = rentAdjust.Value;
        if (sublease is not null) Sublease = sublease;
        if (additionalExpenses is not null) AdditionalExpenses = additionalExpenses;
        if (leaseTimestamp is not null) LeaseTimestamp = leaseTimestamp;
        if (contractRenewal is not null) ContractRenewal = contractRenewal;
        if (rentalTermsImpactingPropertyUse is not null) RentalTermsImpactingPropertyUse = rentalTermsImpactingPropertyUse;
        if (terminationOfLease is not null) TerminationOfLease = terminationOfLease;
        if (remark is not null) Remark = remark;
        if (banking is not null) Banking = banking;
    }
}
