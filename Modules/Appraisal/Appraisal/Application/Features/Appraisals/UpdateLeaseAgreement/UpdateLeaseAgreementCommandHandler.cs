namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreement;

public class UpdateLeaseAgreementCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateLeaseAgreementCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateLeaseAgreementCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.GetProperty(command.PropertyId)
            ?? throw new PropertyNotFoundException(command.PropertyId);

        if (!property.PropertyType.IsLeaseAgreement)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a lease agreement property");

        var detail = property.LeaseAgreementDetail
            ?? throw new InvalidOperationException($"Lease agreement detail not found for property {command.PropertyId}");

        detail.Update(
            lesseeName: command.LesseeName,
            lessorName: command.LessorName,
            leasePeriodAsContract: command.LeasePeriodAsContract,
            remainingLeaseAsAppraisalDate: command.RemainingLeaseAsAppraisalDate,
            contractNo: command.ContractNo,
            leaseStartDate: command.LeaseStartDate,
            leaseEndDate: command.LeaseEndDate,
            leaseRentFee: command.LeaseRentFee,
            rentAdjust: command.RentAdjust,
            sublease: command.Sublease,
            additionalExpenses: command.AdditionalExpenses,
            leaseTerminate: command.LeaseTerminate,
            contractRenewal: command.ContractRenewal,
            rentalTermsImpactingPropertyUse: command.RentalTermsImpactingPropertyUse,
            terminationOfLease: command.TerminationOfLease,
            remark: command.Remark);

        return MediatR.Unit.Value;
    }
}
