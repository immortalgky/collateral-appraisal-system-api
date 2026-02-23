using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.CreateMachineryProperty;

/// <summary>
/// Handler for creating a machinery property with its appraisal detail
/// </summary>
public class CreateMachineryPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<CreateMachineryPropertyCommand, CreateMachineryPropertyResult>
{
    public async Task<CreateMachineryPropertyResult> Handle(
        CreateMachineryPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Execute domain operation via aggregate
        var property = appraisal.AddMachineryProperty(
            command.OwnerName,
            command.Description);

        // 3. Update detail with additional fields
        property.MachineryDetail!.Update(
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

        // 4. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        // 5. Return both IDs
        return new CreateMachineryPropertyResult(property.Id, property.MachineryDetail.Id);
    }
}
