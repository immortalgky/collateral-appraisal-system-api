using Appraisal.Application.Features.Appraisals.Shared;
using Shared.Time;

namespace Appraisal.Application.Features.Appraisals.UpdateCondoProperty;

/// <summary>
/// Handler for updating a condo pma property detail
/// </summary>
public class UpdateCondoPMAPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<UpdateCondoPMAPropertyCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateCondoPMAPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        CondoPmaApplier.Apply(
            appraisal,
            command.PropertyId,
            command.SellingPrice,
            command.ForcedSalePrice,
            command.BuildingInsurancePrice,
            command.CondoName,
            command.BuiltOnTitleNumber,
            command.CondoRegistrationNumber,
            command.RoomNumber,
            command.FloorNumber,
            command.BuildingNumber,
            command.SubDistrict,
            command.District,
            command.Province,
            dateTimeProvider);

        // Push the updated PMA to the external LOS system asynchronously (outbox → integration
        // event → webhook, delivered by the Integration module). Save stays atomic — the outbox
        // row commits in the same transaction as the PMA data (TransactionalBehavior).
        appraisal.MarkPmaUpdated(command.PropertyId);

        // Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return MediatR.Unit.Value;
    }
}
