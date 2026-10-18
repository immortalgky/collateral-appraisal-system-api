using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdateVesselProperty;

/// <summary>
/// Writes an <see cref="UpdateVesselPropertyCommand"/> payload into the property. Shared by the real page handler and
/// the data-correction command so both apply exactly the same rules; the side effects that belong
/// to the real page (valuation recompute, insurance derivation) stay in the handler.
/// </summary>
public static class VesselPropertyApplier
{
    public static void Apply(AppraisalProperty property, UpdateVesselPropertyCommand command)
    {
        // 3. Validate property type
        if (property.PropertyType != PropertyType.Vessel)
            throw new InvalidOperationException($"Property {property.Id} is not a vessel property");

        // 4. Get the vessel detail
        var detail = property.VesselDetail
            ?? throw new InvalidOperationException($"Vessel detail not found for property {property.Id}");

        // 5. Update via domain method
        detail.Update(
            propertyName: command.PropertyName,
            vesselName: command.VesselName,
            engineNo: command.EngineNo,
            registrationNumber: command.RegistrationNumber,
            registrationDate: command.RegistrationDate,
            brand: command.Brand,
            model: command.Model,
            yearOfManufacture: command.YearOfManufacture,
            placeOfManufacture: command.PlaceOfManufacture,
            vesselType: command.VesselType,
            classOfVessel: command.ClassOfVessel,
            purchaseDate: command.PurchaseDate,
            purchasePrice: command.PurchasePrice,
            engineCapacity: command.EngineCapacity,
            width: command.Width,
            length: command.Length,
            height: command.Height,
            grossTonnage: command.GrossTonnage,
            netTonnage: command.NetTonnage,
            energyUse: command.EnergyUse,
            energyUseRemark: command.EnergyUseRemark,
            ownerName: command.OwnerName,
            isOwnerVerified: command.IsOwnerVerified,
            canUse: command.CanUse,
            formerName: command.FormerName,
            vesselCurrentName: command.VesselCurrentName,
            location: command.Location,
            conditionUse: command.ConditionUse,
            vesselCondition: command.VesselCondition,
            vesselAge: command.VesselAge,
            vesselEfficiency: command.VesselEfficiency,
            vesselTechnology: command.VesselTechnology,
            usePurpose: command.UsePurpose,
            vesselPart: command.VesselPart,
            remark: command.Remark,
            other: command.Other,
            appraiserOpinion: command.AppraiserOpinion);
    }
}
