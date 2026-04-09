namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreement;

public record UpdateLeaseAgreementRequest(
    string? LesseeName = null,
    string? TenantName = null,
    decimal? LeasePeriodAsContract = null,
    decimal? RemainingLeaseAsAppraisalDate = null,
    string? ContractNo = null,
    DateTime? LeaseStartDate = null,
    DateTime? LeaseEndDate = null,
    decimal? LeaseRentFee = null,
    decimal? RentAdjust = null,
    string? Sublease = null,
    decimal? AdditionalExpenses = null,
    string? LeaseTerminate = null,
    string? ContractRenewal = null,
    string? RentalTermsImpactingPropertyUse = null,
    string? TerminationOfLease = null,
    string? Remark = null
    );
