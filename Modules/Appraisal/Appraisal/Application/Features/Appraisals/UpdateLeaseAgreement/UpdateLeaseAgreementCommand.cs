using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreement;

public record UpdateLeaseAgreementCommand(
    Guid AppraisalId,
    Guid PropertyId,
    string? LesseeName = null,
    string? LessorName = null,
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
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
