using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Shared.Time;

namespace Appraisal.Application.Features.Appraisals.Shared;

/// <summary>
/// Shared mutation logic for saving a Land/Building PMA property (selling/forced-sale/building
/// insurance price, address, titles). Used by both the full-save command (which also raises
/// <see cref="Appraisal.Domain.Appraisals.Appraisal.MarkPmaUpdated"/> to trigger the LOS webhook)
/// and the draft-save command (which persists + stamps ExternalSyncStatus=Pending only, with no
/// webhook trigger). Keeping the mutation logic here avoids duplicating it between the two handlers.
/// </summary>
public static class LandPmaApplier
{
    public static void Apply(
        Domain.Appraisals.Appraisal appraisal,
        Guid propertyId,
        decimal? sellingPrice,
        decimal? forcedSalePrice,
        decimal? buildingInsurancePrice,
        List<LandTitleItemData>? titles,
        string? subDistrict,
        string? district,
        string? province,
        IDateTimeProvider dateTimeProvider)
    {
        var property = appraisal.GetProperty(propertyId)
                       ?? throw new PropertyNotFoundException(propertyId);

        if (property.PropertyType != PropertyType.LandAndBuilding && property.PropertyType != PropertyType.LeaseAgreementLandAndBuilding)
            throw new InvalidOperationException($"Property {propertyId} is not a land and building property");

        var landDetail = property.LandDetail
                         ?? throw new InvalidOperationException(
                             $"Land detail not found for property {propertyId}");

        property.UpdatePrice(
            sellingPrice: sellingPrice,
            forcedSalePrice: forcedSalePrice,
            buildingInsurancePrice: buildingInsurancePrice
        );

        AdministrativeAddress? address = null;
        if (subDistrict is not null || district is not null || province is not null)
            address = AdministrativeAddress.Create(
                subDistrict,
                district,
                province
            );
        landDetail.UpdatePmaFields(
            address: address
        );

        // Sync land titles (null = no-op, empty list = clear all)
        if (titles is not null)
            SyncTitles(landDetail, titles);

        // Stamp Pending — callers decide whether to also raise MarkPmaUpdated (full save only).
        property.SetExternalSyncStatus(ExternalSyncStatuses.Pending, null, dateTimeProvider.ApplicationNow);
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
