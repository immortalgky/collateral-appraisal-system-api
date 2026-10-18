using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;
using Appraisal.Application.Services;

namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementBuildingProperty;

/// <summary>
/// Handler for updating a lease agreement building property detail
/// </summary>
public class UpdateLeaseAgreementBuildingPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalValuationSummaryService valuationSummaryService
) : ICommandHandler<UpdateLeaseAgreementBuildingPropertyCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateLeaseAgreementBuildingPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.GetProperty(command.PropertyId)
            ?? throw new PropertyNotFoundException(command.PropertyId);

        LeaseAgreementBuildingPropertyApplier.Apply(property, command);

        await valuationSummaryService.RecomputeAsync(command.AppraisalId, cancellationToken);

        return MediatR.Unit.Value;
    }
}
