using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.CreateMachineryProperty;

/// <summary>
/// Handler for creating a machinery property with its appraisal detail
/// </summary>
public class CreateMachineryPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
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
        var property = appraisal.AddMachineryProperty();

        // 3. Update detail with additional fields
        property.MachineryDetail!.Update(
            command.PropertyName,
            command.MachineName,
            command.EngineNo,
            command.ChassisNo,
            command.RegistrationNo,
            command.Brand,
            command.Model,
            command.Series,
            command.YearOfManufacture,
            command.Manufacturer,
            command.PurchaseDate,
            command.PurchasePrice,
            command.Capacity,
            command.Quantity,
            command.MachineDimensions,
            command.Width,
            command.Length,
            command.Height,
            command.EnergyUse,
            command.EnergyUseRemark,
            command.OwnerName,
            conditionUse: command.ConditionUse,
            machineCondition: command.MachineCondition,
            machineAge: command.MachineAge,
            machineEfficiency: command.MachineEfficiency,
            machineTechnology: command.MachineTechnology,
            usagePurpose: command.UsagePurpose,
            machineParts: command.MachineParts,
            replacementValue: command.ReplacementValue,
            conditionValue: command.ConditionValue,
            remark: command.Remark,
            other: command.Other,
            appraiserOpinion: command.AppraiserOpinion);

        // 4. Save aggregate
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (command.GroupId.HasValue) appraisal.AddPropertyToGroup(command.GroupId.Value, property.Id);

        // 5. Return both IDs
        return new CreateMachineryPropertyResult(property.Id, property.MachineryDetail.Id);
    }
}