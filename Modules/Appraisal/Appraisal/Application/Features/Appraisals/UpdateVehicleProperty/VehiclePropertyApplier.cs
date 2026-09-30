using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdateVehicleProperty;

/// <summary>
/// Writes an <see cref="UpdateVehiclePropertyCommand"/> payload into the property. Shared by the real page handler and
/// the data-correction command so both apply exactly the same rules; the side effects that belong
/// to the real page (valuation recompute, insurance derivation) stay in the handler.
/// </summary>
public static class VehiclePropertyApplier
{
    public static void Apply(AppraisalProperty property, UpdateVehiclePropertyCommand command)
    {
        // 3. Validate property type
        if (property.PropertyType != PropertyType.Vehicle)
            throw new InvalidOperationException($"Property {property.Id} is not a vehicle property");

        // 4. Get the vehicle detail
        var detail = property.VehicleDetail
            ?? throw new InvalidOperationException($"Vehicle detail not found for property {property.Id}");

        // 5. Update via domain method
        detail.Update(
            propertyName: command.PropertyName,
            vehicleName: command.VehicleName,
            engineNo: command.EngineNo,
            chassisNo: command.ChassisNo,
            registrationNumber: command.RegistrationNumber,
            brand: command.Brand,
            model: command.Model,
            yearOfManufacture: command.YearOfManufacture,
            countryOfManufacture: command.CountryOfManufacture,
            purchaseDate: command.PurchaseDate,
            purchasePrice: command.PurchasePrice,
            capacity: command.Capacity,
            width: command.Width,
            length: command.Length,
            height: command.Height,
            energyUse: command.EnergyUse,
            energyUseRemark: command.EnergyUseRemark,
            ownerName: command.OwnerName,
            isOwnerVerified: command.IsOwnerVerified,
            canUse: command.CanUse,
            location: command.Location,
            conditionUse: command.ConditionUse,
            vehicleCondition: command.VehicleCondition,
            vehicleAge: command.VehicleAge,
            vehicleEfficiency: command.VehicleEfficiency,
            vehicleTechnology: command.VehicleTechnology,
            usePurpose: command.UsePurpose,
            vehiclePart: command.VehiclePart,
            remark: command.Remark,
            other: command.Other,
            appraiserOpinion: command.AppraiserOpinion);
    }
}
