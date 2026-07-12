using Shared.Time;

namespace Appraisal.Application.Features.Appraisals.Shared;

/// <summary>
/// Shared mutation logic for saving a Condo PMA property (selling/forced-sale/building
/// insurance price, condo detail, address). Used by both the full-save command (which also
/// raises <see cref="Appraisal.Domain.Appraisals.Appraisal.MarkPmaUpdated"/> to trigger the LOS
/// webhook) and the draft-save command (which persists + stamps ExternalSyncStatus=Pending only,
/// with no webhook trigger). Keeping the mutation logic here avoids duplicating it between the
/// two handlers.
/// </summary>
public static class CondoPmaApplier
{
    public static void Apply(
        Domain.Appraisals.Appraisal appraisal,
        Guid propertyId,
        decimal? sellingPrice,
        decimal? forcedSalePrice,
        decimal? buildingInsurancePrice,
        string? condoName,
        string? builtOnTitleNumber,
        string? condoRegistrationNumber,
        string? roomNumber,
        string? floorNumber,
        string? buildingNumber,
        string? subDistrict,
        string? district,
        string? province,
        IDateTimeProvider dateTimeProvider)
    {
        var property = appraisal.GetProperty(propertyId)
                       ?? throw new PropertyNotFoundException(propertyId);

        if (property.PropertyType != PropertyType.Condo)
            throw new InvalidOperationException($"Property {propertyId} is not a condo property");

        var detail = property.CondoDetail
                     ?? throw new InvalidOperationException($"Condo detail not found for property {propertyId}");

        AdministrativeAddress? address = null;
        if (subDistrict is not null || district is not null || province is not null)
        {
            address = AdministrativeAddress.Create(
                subDistrict,
                district,
                province
            );
        }
        property.UpdatePrice(
            sellingPrice: sellingPrice,
            forcedSalePrice: forcedSalePrice,
            buildingInsurancePrice: buildingInsurancePrice
        );

        detail.Update(
            condoName: condoName,
            ownerName: "",
            buildingNumber: buildingNumber,
            builtOnTitleNumber: builtOnTitleNumber,
            condoRegistrationNumber: condoRegistrationNumber,
            roomNumber: roomNumber,
            floorNumber: floorNumber,
            address: address
        );

        // Stamp Pending — callers decide whether to also raise MarkPmaUpdated (full save only).
        property.SetExternalSyncStatus(ExternalSyncStatuses.Pending, null, dateTimeProvider.ApplicationNow);
    }
}
