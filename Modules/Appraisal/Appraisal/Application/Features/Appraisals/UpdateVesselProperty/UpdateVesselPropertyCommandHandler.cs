using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdateVesselProperty;

/// <summary>
/// Handler for updating a vessel property detail
/// </summary>
public class UpdateVesselPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateVesselPropertyCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateVesselPropertyCommand command,
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
        if (property.PropertyType != PropertyType.Vessel)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a vessel property");

        // 4. Get the vessel detail
        var detail = property.VesselDetail
            ?? throw new InvalidOperationException($"Vessel detail not found for property {command.PropertyId}");

        // 5. Update via domain method
        detail.Update(
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

        // 6. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return MediatR.Unit.Value;
    }
}
