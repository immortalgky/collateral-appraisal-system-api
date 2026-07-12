using Appraisal.Application.Features.Appraisals.Shared;
using Shared.Time;

namespace Appraisal.Application.Features.Appraisals.SaveCondoPMAPropertyDraft;

/// <summary>
/// Handler for saving a condo pma property detail as a draft. Persists the data and stamps
/// ExternalSyncStatus=Pending, but does NOT raise <see cref="Domain.Appraisals.Appraisal.MarkPmaUpdated"/> — so no
/// LOS webhook is triggered. Use
/// <see cref="UpdateCondoProperty.UpdateCondoPMAPropertyCommand"/> for the full save that pushes
/// to LOS.
/// </summary>
public class SaveCondoPMAPropertyDraftCommandHandler(
    IAppraisalRepository appraisalRepository,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<SaveCondoPMAPropertyDraftCommand>
{
    public async Task<MediatR.Unit> Handle(
        SaveCondoPMAPropertyDraftCommand command,
        CancellationToken cancellationToken)
    {
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

        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return MediatR.Unit.Value;
    }
}
