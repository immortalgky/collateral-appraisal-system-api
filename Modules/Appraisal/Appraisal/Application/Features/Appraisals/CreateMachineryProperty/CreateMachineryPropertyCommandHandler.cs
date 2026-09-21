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
            propertyName: command.PropertyName,
            machineName: command.MachineName,
            engineNo: command.EngineNo,
            chassisNo: command.ChassisNo,
            registrationNumber: command.RegistrationNumber,
            serialNo: command.SerialNo,
            registrationStatus: command.RegistrationStatus,
            installationStatus: command.InstallationStatus,
            machineType: command.MachineType,
            invoiceNumber: command.InvoiceNumber,
            isPriceCertified: command.IsPriceCertified,
            brand: command.Brand,
            model: command.Model,
            series: command.Series,
            yearOfManufacture: command.YearOfManufacture,
            manufacturer: command.Manufacturer,
            purchaseDate: command.PurchaseDate,
            purchasePrice: command.PurchasePrice,
            capacity: command.Capacity,
            quantity: command.Quantity,
            machineDimensions: command.MachineDimensions,
            width: command.Width,
            length: command.Length,
            height: command.Height,
            energyUse: command.EnergyUse,
            energyUseRemark: command.EnergyUseRemark,
            ownerName: command.OwnerName,
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