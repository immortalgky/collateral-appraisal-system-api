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
            appraisal: appraisal,
            propertyId: command.PropertyId,
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            buildingInsurancePrice: command.BuildingInsurancePrice,
            condoName: command.CondoName,
            titleNumber: command.TitleNumber,
            condoRegistrationNumber: command.CondoRegistrationNumber,
            roomNumber: command.RoomNumber,
            floorNumber: command.FloorNumber,
            buildingNumber: command.BuildingNumber,
            subDistrict: command.SubDistrict,
            district: command.District,
            province: command.Province,
            dateTimeProvider: dateTimeProvider);

        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return MediatR.Unit.Value;
    }
}
