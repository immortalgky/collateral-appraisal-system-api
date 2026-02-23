using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetVehicleProperty;

/// <summary>
/// Handler for getting a vehicle property with its detail
/// </summary>
public class GetVehiclePropertyQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetVehiclePropertyQuery, GetVehiclePropertyResult>
{
    public async Task<GetVehiclePropertyResult> Handle(
        GetVehiclePropertyQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            query.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(query.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(query.PropertyId)
            ?? throw new PropertyNotFoundException(query.PropertyId);

        // 3. Validate property type
        if (property.PropertyType != PropertyType.Vehicle)
            throw new InvalidOperationException($"Property {query.PropertyId} is not a vehicle property");

        // 4. Get the vehicle detail
        var detail = property.VehicleDetail
            ?? throw new InvalidOperationException($"Vehicle detail not found for property {query.PropertyId}");

        // 5. Map to result
        return new GetVehiclePropertyResult(
            PropertyId: property.Id,
            AppraisalId: property.AppraisalId,
            SequenceNumber: property.SequenceNumber,
            PropertyType: property.PropertyType.ToString(),
            Description: property.Description,
            DetailId: detail.Id,
            PropertyName: detail.PropertyName,
            VehicleName: detail.VehicleName,
            EngineNo: detail.EngineNo,
            ChassisNo: detail.ChassisNo,
            RegistrationNo: detail.RegistrationNo,
            Brand: detail.Brand,
            Model: detail.Model,
            YearOfManufacture: detail.YearOfManufacture,
            CountryOfManufacture: detail.CountryOfManufacture,
            PurchaseDate: detail.PurchaseDate,
            PurchasePrice: detail.PurchasePrice,
            Capacity: detail.Capacity,
            Width: detail.Width,
            Length: detail.Length,
            Height: detail.Height,
            EnergyUse: detail.EnergyUse,
            EnergyUseRemark: detail.EnergyUseRemark,
            Owner: detail.OwnerName,
            VerifiableOwner: detail.IsOwnerVerified,
            CanUse: detail.CanUse,
            Location: detail.Location,
            ConditionUse: detail.ConditionUse,
            VehicleCondition: detail.VehicleCondition,
            VehicleAge: detail.VehicleAge,
            VehicleEfficiency: detail.VehicleEfficiency,
            VehicleTechnology: detail.VehicleTechnology,
            UsePurpose: detail.UsePurpose,
            VehiclePart: detail.VehiclePart,
            Remark: detail.Remark,
            Other: detail.Other,
            AppraiserOpinion: detail.AppraiserOpinion);
    }
}
