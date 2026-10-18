using Appraisal.Application.Features.Appraisals.CreateLandProperty;

namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementLandProperty;

/// <summary>
/// Handler for updating a lease agreement land property detail
/// </summary>
public class UpdateLeaseAgreementLandPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateLeaseAgreementLandPropertyCommand>
{
    public async Task<Unit> Handle(
        UpdateLeaseAgreementLandPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.GetProperty(command.PropertyId)
                       ?? throw new PropertyNotFoundException(command.PropertyId);

        LeaseAgreementLandPropertyApplier.Apply(property, command);

        return Unit.Value;
    }
}
