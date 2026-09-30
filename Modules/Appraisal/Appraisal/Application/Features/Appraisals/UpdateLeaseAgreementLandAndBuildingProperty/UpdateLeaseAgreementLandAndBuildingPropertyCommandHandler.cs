using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;
using Appraisal.Application.Services;

namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementLandAndBuildingProperty;

/// <summary>
/// Handler for updating a lease agreement land and building property detail
/// </summary>
public class UpdateLeaseAgreementLandAndBuildingPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalValuationSummaryService valuationSummaryService
) : ICommandHandler<UpdateLeaseAgreementLandAndBuildingPropertyCommand>
{
    public async Task<Unit> Handle(
        UpdateLeaseAgreementLandAndBuildingPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(command.PropertyId)
                       ?? throw new PropertyNotFoundException(command.PropertyId);

        LeaseAgreementLandAndBuildingPropertyApplier.Apply(property, command);

        // 11. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        await valuationSummaryService.RecomputeAsync(command.AppraisalId, cancellationToken);

        return Unit.Value;
    }
}
