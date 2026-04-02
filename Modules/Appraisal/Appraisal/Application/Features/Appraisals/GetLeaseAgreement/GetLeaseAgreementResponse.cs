namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreement;

public record GetLeaseAgreementResponse(
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
