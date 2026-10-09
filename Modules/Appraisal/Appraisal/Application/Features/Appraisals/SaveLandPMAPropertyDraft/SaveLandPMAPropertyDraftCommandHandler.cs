using Appraisal.Application.Features.Appraisals.Shared;
using Shared.Time;

namespace Appraisal.Application.Features.Appraisals.SaveLandPMAPropertyDraft;

/// <summary>
/// Handler for saving a land pma property detail as a draft. Persists the data and stamps
/// ExternalSyncStatus=Pending, but does NOT raise <see cref="Domain.Appraisals.Appraisal.MarkPmaUpdated"/> — so no
/// LOS webhook is triggered. Use <see cref="UpdateLandPMAProperty.UpdateLandPMAPropertyCommand"/>
/// for the full save that pushes to LOS.
/// </summary>
public class SaveLandPMAPropertyDraftCommandHandler(
    IAppraisalRepository appraisalRepository,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<SaveLandPMAPropertyDraftCommand>
{
    public async Task<Unit> Handle(
        SaveLandPMAPropertyDraftCommand command,
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

        return Unit.Value;
    }
}
