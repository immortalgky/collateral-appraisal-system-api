
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
            appraisal,
            command.PropertyId,
            command.SellingPrice,
            command.ForcedSalePrice,
            command.BuildingInsurancePrice,
            command.Titles,
            command.SubDistrict,
            command.District,
            command.Province,
            dateTimeProvider);

        // Push the updated PMA to the external LOS system asynchronously (outbox → integration
        // event → webhook, delivered by the Integration module). Save stays atomic — the outbox
        // row commits in the same transaction as the PMA data (TransactionalBehavior).
        appraisal.MarkPmaUpdated(command.PropertyId);

        return Unit.Value;
    }
}
