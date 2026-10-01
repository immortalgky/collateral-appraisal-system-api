using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;
using Appraisal.Application.Services;

namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementCondoProperty;

/// <summary>
/// Handler for updating a lease agreement condo property detail
/// </summary>
public class UpdateLeaseAgreementCondoPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    ISender mediator,
    AppraisalValuationSummaryService valuationSummaryService
) : ICommandHandler<UpdateLeaseAgreementCondoPropertyCommand>
{
    public async Task<Unit> Handle(
        UpdateLeaseAgreementCondoPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(command.PropertyId)
                       ?? throw new PropertyNotFoundException(command.PropertyId);

        // Derive BuildingInsurancePrice from the selected fire-insurance condition
        // (Parameter-module reference rate × UsableArea) — never taken from the client directly.
        var buildingInsurancePrice = await CondoFireInsuranceCalculator.DeriveBuildingInsurancePriceAsync(
            mediator, command.FireInsuranceCode, command.UsableArea, cancellationToken);

        LeaseAgreementCondoPropertyApplier.Apply(property, command, buildingInsurancePrice);

        // 10. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        await valuationSummaryService.RecomputeAsync(command.AppraisalId, cancellationToken);

        return Unit.Value;
    }
}
