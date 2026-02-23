using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdateMachineryProperty;

/// <summary>
/// Handler for updating a machinery property detail
/// </summary>
public class UpdateMachineryPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateMachineryPropertyCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateMachineryPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(command.PropertyId)
            ?? throw new PropertyNotFoundException(command.PropertyId);

        // 3. Validate property type
        if (property.PropertyType != PropertyType.Machinery)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a machinery property");

        // 4. Get the machinery detail
        var detail = property.MachineryDetail
            ?? throw new InvalidOperationException($"Machinery detail not found for property {command.PropertyId}");

        // 5. Update via domain method
        detail.Update(
            propertyName: command.PropertyName,
            machineName: command.MachineName,
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
            ownerName: command.OwnerName,
            isOwnerVerified: command.IsOwnerVerified,
            canUse: command.CanUse,
            location: command.Location,
            conditionUse: command.ConditionUse,
            machineCondition: command.MachineCondition,
            machineAge: command.MachineAge,
            machineEfficiency: command.MachineEfficiency,
            machineTechnology: command.MachineTechnology,
            usePurpose: command.UsePurpose,
            machinePart: command.MachinePart,
            remark: command.Remark,
            other: command.Other,
            appraiserOpinion: command.AppraiserOpinion);

        // 6. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return MediatR.Unit.Value;
    }
}
