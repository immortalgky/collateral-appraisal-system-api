using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetVesselProperty;

/// <summary>
/// Handler for getting a vessel property with its detail
/// </summary>
public class GetVesselPropertyQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetVesselPropertyQuery, GetVesselPropertyResult>
{
    public async Task<GetVesselPropertyResult> Handle(
        GetVesselPropertyQuery query,
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
        if (property.PropertyType != PropertyType.Vessel)
            throw new InvalidOperationException($"Property {query.PropertyId} is not a vessel property");

        // 4. Get the vessel detail
        var detail = property.VesselDetail
            ?? throw new InvalidOperationException($"Vessel detail not found for property {query.PropertyId}");

        // 5. Map to result
        return new GetVesselPropertyResult(
            PropertyId: property.Id,
            AppraisalId: property.AppraisalId,
            SequenceNumber: property.SequenceNumber,
            PropertyType: property.PropertyType.ToString(),
            Description: property.Description,
            DetailId: detail.Id,
            PropertyName: detail.PropertyName,
            VesselName: detail.VesselName,
            EngineNo: detail.EngineNo,
            RegistrationNo: detail.RegistrationNo,
            RegistrationDate: detail.RegistrationDate,
            Brand: detail.Brand,
            Model: detail.Model,
            YearOfManufacture: detail.YearOfManufacture,
            PlaceOfManufacture: detail.PlaceOfManufacture,
            VesselType: detail.VesselType,
            ClassOfVessel: detail.ClassOfVessel,
            PurchaseDate: detail.PurchaseDate,
            PurchasePrice: detail.PurchasePrice,
            EngineCapacity: detail.EngineCapacity,
            Width: detail.Width,
            Length: detail.Length,
            Height: detail.Height,
            GrossTonnage: detail.GrossTonnage,
            NetTonnage: detail.NetTonnage,
            EnergyUse: detail.EnergyUse,
            EnergyUseRemark: detail.EnergyUseRemark,
            Owner: detail.OwnerName,
            VerifiableOwner: detail.IsOwnerVerified,
            CanUse: detail.CanUse,
            FormerName: detail.FormerName,
            VesselCurrentName: detail.VesselCurrentName,
            Location: detail.Location,
            ConditionUse: detail.ConditionUse,
            VesselCondition: detail.VesselCondition,
            VesselAge: detail.VesselAge,
            VesselEfficiency: detail.VesselEfficiency,
            VesselTechnology: detail.VesselTechnology,
            UsePurpose: detail.UsePurpose,
            VesselPart: detail.VesselPart,
            Remark: detail.Remark,
            Other: detail.Other,
            AppraiserOpinion: detail.AppraiserOpinion);
    }
}
