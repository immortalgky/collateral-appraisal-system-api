
using Appraisal.Application.Features.Appraisals.CreateLandProperty;

namespace Appraisal.Application.Features.Appraisals.UpdateLandPMAProperty;

/// <summary>
/// Handler for updating a land property detail
/// </summary>
public class UpdateLandPMAPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateLandPMAPropertyCommand>
{
    public async Task<Unit> Handle(
        UpdateLandPMAPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.GetProperty(command.PropertyId)
                       ?? throw new PropertyNotFoundException(command.PropertyId);

        if (property.PropertyType != PropertyType.LandAndBuilding && property.PropertyType != PropertyType.LeaseAgreementLandAndBuilding)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a land and building property");

        var landDetail = property.LandDetail
                         ?? throw new InvalidOperationException(
                             $"Land detail not found for property {command.PropertyId}");

        property.UpdatePrice(
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            buildingInsurancePrice: command.BuildingInsurancePrice
        );

        AdministrativeAddress? address = null;
        if (command.SubDistrict is not null || command.District is not null ||
            command.Province is not null)
            address = AdministrativeAddress.Create(
                command.SubDistrict,
                command.District,
                command.Province
            );
        landDetail.Update(
            address: address
        );


        // Sync land titles (null = no-op, empty list = clear all)
        if (command.Titles is not null)
            SyncTitles(landDetail, command.Titles);

        return Unit.Value;
    }

    private static void SyncTitles(LandAppraisalDetail landDetail, List<LandTitleItemData> incomingTitles)
    {
        var incomingIds = incomingTitles
            .Where(t => t.Id.HasValue)
            .Select(t => t.Id!.Value)
            .ToHashSet();

        // Delete titles not in the incoming list
        var titlesToRemove = landDetail.Titles
            .Where(t => !incomingIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToList();
        foreach (var id in titlesToRemove)
            landDetail.RemoveTitle(id);

        // Add or update
        foreach (var titleData in incomingTitles)
        {
            LandArea? area = null;
            if (titleData.Rai.HasValue || titleData.Ngan.HasValue || titleData.SquareWa.HasValue)
                area = LandArea.Create(titleData.Rai, titleData.Ngan, titleData.SquareWa);

            if (titleData.Id.HasValue)
            {
                // Update existing
                var existing = landDetail.Titles.FirstOrDefault(t => t.Id == titleData.Id.Value);
                existing?.Update(
                    titleData.BookNumber, titleData.PageNumber,
                    titleData.LandParcelNumber, titleData.SurveyNumber,
                    titleData.MapSheetNumber, titleData.Rawang,
                    titleData.AerialMapName, titleData.AerialMapNumber,
                    area, titleData.BoundaryMarkerType, titleData.BoundaryMarkerRemark,
                    titleData.DocumentValidationResultType, titleData.IsMissingFromSurvey,
                    titleData.GovernmentPricePerSqWa, titleData.GovernmentPrice,
                    titleData.Remark);
            }
            else
            {
                // Create new
                var title = LandTitle.Create(landDetail.Id, titleData.TitleNumber, titleData.TitleType ?? "DEED");
                title.Update(
                    titleData.BookNumber, titleData.PageNumber,
                    titleData.LandParcelNumber, titleData.SurveyNumber,
                    titleData.MapSheetNumber, titleData.Rawang,
                    titleData.AerialMapName, titleData.AerialMapNumber,
                    area, titleData.BoundaryMarkerType, titleData.BoundaryMarkerRemark,
                    titleData.DocumentValidationResultType, titleData.IsMissingFromSurvey,
                    titleData.GovernmentPricePerSqWa, titleData.GovernmentPrice,
                    titleData.Remark);
                landDetail.AddTitle(title);
            }
        }
    }
}