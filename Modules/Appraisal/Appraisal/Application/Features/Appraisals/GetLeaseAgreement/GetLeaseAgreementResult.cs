namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreement;

public record GetLeaseAgreementResult(
    Guid DetailId,
    Guid AppraisalPropertyId,
    string? LesseeName,
    string? LessorName,
    decimal? LeasePeriodAsContract,
    decimal? RemainingLeaseAsAppraisalDate,
    string? ContractNo,
    DateTime? LeaseStartDate,
    DateTime? LeaseEndDate,
    decimal? LeaseRentFee,
    decimal? RentAdjust,
    string? Sublease,
    decimal? AdditionalExpenses,
    string? LeaseTerminate,
    string? ContractRenewal,
    string? RentalTermsImpactingPropertyUse,
    string? TerminationOfLease,
    string? Remark
);
