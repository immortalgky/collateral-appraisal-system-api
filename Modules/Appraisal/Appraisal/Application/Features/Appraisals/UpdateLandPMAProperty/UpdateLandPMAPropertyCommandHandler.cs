
using Appraisal.Application.Features.Appraisals.Shared;
using Shared.Time;

namespace Appraisal.Application.Features.Appraisals.UpdateLandPMAProperty;

/// <summary>
/// Handler for updating a land property detail
/// </summary>
public class UpdateLandPMAPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<UpdateLandPMAPropertyCommand>
{
    public async Task<Unit> Handle(
        UpdateLandPMAPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        LandPmaApplier.Apply(
            appraisal: appraisal,
            propertyId: command.PropertyId,
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            buildingInsurancePrice: command.BuildingInsurancePrice,
            titles: command.Titles,
            subDistrict: command.SubDistrict,
            district: command.District,
            province: command.Province,
            dateTimeProvider: dateTimeProvider);

        // Push the updated PMA to the external LOS system asynchronously (outbox → integration
        // event → webhook, delivered by the Integration module). Save stays atomic — the outbox
        // row commits in the same transaction as the PMA data (TransactionalBehavior).
        appraisal.MarkPmaUpdated(command.PropertyId);

        return Unit.Value;
    }
}
