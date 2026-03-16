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
    public async Task<Unit> Handle(
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
                     ?? throw new InvalidOperationException(
                         $"Machinery detail not found for property {command.PropertyId}");

        // 5. Update via domain method
        detail.Update(
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
            command.IsOwnerVerified,
            command.IsOperational,
            command.Location,
            command.ConditionUse,
            command.MachineCondition,
            command.MachineAge,
            command.MachineEfficiency,
            command.MachineTechnology,
            command.UsagePurpose,
            command.MachineParts,
            command.ReplacementValue,
            command.ConditionValue,
            command.Remark,
            command.Other,
            command.AppraiserOpinion);

        return Unit.Value;
    }
}