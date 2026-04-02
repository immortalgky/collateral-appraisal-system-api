namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreement;

public record UpdateLeaseAgreementRequest(
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
