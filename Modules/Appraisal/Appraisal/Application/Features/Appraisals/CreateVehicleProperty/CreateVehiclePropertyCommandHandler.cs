using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.CreateVehicleProperty;

/// <summary>
/// Handler for creating a vehicle property with its appraisal detail
/// </summary>
public class CreateVehiclePropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<CreateVehiclePropertyCommand, CreateVehiclePropertyResult>
{
    public async Task<CreateVehiclePropertyResult> Handle(
        CreateVehiclePropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Execute domain operation via aggregate
        var property = appraisal.AddVehicleProperty(
            command.OwnerName,
            command.Description);

        // 3. Update detail with additional fields
        property.VehicleDetail!.Update(
            propertyName: command.PropertyName,
            vehicleName: command.VehicleName,
            engineNo: command.EngineNo,
            chassisNo: command.ChassisNo,
            registrationNo: command.RegistrationNo,
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

        // 4. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        // 5. Return both IDs
        return new CreateVehiclePropertyResult(property.Id, property.VehicleDetail.Id);
    }
}
