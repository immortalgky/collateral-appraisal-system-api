namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreement;

public class GetLeaseAgreementQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetLeaseAgreementQuery, GetLeaseAgreementResult>
{
    public async Task<GetLeaseAgreementResult> Handle(
        GetLeaseAgreementQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            query.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(query.AppraisalId);

        var property = appraisal.GetProperty(query.PropertyId)
            ?? throw new PropertyNotFoundException(query.PropertyId);

        // LeaseAgreementDetail is present on lease-agreement properties and on plain
        // land (L/LB) flagged "rented out to others" — both are valid sources.
        var detail = property.LeaseAgreementDetail
            ?? throw new InvalidOperationException($"Lease agreement detail not found for property {query.PropertyId}");

        return new GetLeaseAgreementResult(
            DetailId: detail.Id,
            AppraisalPropertyId: detail.AppraisalPropertyId,
            LesseeName: detail.LesseeName,
            LessorName: detail.LessorName,
            LeasePeriodAsContract: detail.LeasePeriodAsContract,
            RemainingLeaseAsAppraisalDate: detail.RemainingLeaseAsAppraisalDate,
            ContractNo: detail.ContractNo,
            LeaseStartDate: detail.LeaseStartDate,
            LeaseEndDate: detail.LeaseEndDate,
            LeaseRentFee: detail.LeaseRentFee,
            RentAdjust: detail.RentAdjust,
            Sublease: detail.Sublease,
            AdditionalExpenses: detail.AdditionalExpenses,
            LeaseTerminate: detail.LeaseTerminate,
            ContractRenewal: detail.ContractRenewal,
            RentalTermsImpactingPropertyUse: detail.RentalTermsImpactingPropertyUse,
            TerminationOfLease: detail.TerminationOfLease,
            Remark: detail.Remark);
    }
}
