using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.CreateVesselProperty;

/// <summary>
/// Handler for creating a vessel property with its appraisal detail
/// </summary>
public class CreateVesselPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<CreateVesselPropertyCommand, CreateVesselPropertyResult>
{
    public async Task<CreateVesselPropertyResult> Handle(
        CreateVesselPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Execute domain operation via aggregate
        var property = appraisal.AddVesselProperty(
            command.OwnerName,
            command.Description);

        // 3. Update detail with additional fields
        property.VesselDetail!.Update(
            propertyName: command.PropertyName,
            vesselName: command.VesselName,
            engineNo: command.EngineNo,
            registrationNo: command.RegistrationNo,
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

        // 4. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        // 5. Return both IDs
        return new CreateVesselPropertyResult(property.Id, property.VesselDetail.Id);
    }
}
