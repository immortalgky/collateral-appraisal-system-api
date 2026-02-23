using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetMachineryProperty;

/// <summary>
/// Handler for getting a machinery property with its detail
/// </summary>
public class GetMachineryPropertyQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetMachineryPropertyQuery, GetMachineryPropertyResult>
{
    public async Task<GetMachineryPropertyResult> Handle(
        GetMachineryPropertyQuery query,
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
        if (property.PropertyType != PropertyType.Machinery)
            throw new InvalidOperationException($"Property {query.PropertyId} is not a machinery property");

        // 4. Get the machinery detail
        var detail = property.MachineryDetail
            ?? throw new InvalidOperationException($"Machinery detail not found for property {query.PropertyId}");

        // 5. Map to result
        return new GetMachineryPropertyResult(
            PropertyId: property.Id,
            AppraisalId: property.AppraisalId,
            SequenceNumber: property.SequenceNumber,
            PropertyType: property.PropertyType.ToString(),
            Description: property.Description,
            DetailId: detail.Id,
            PropertyName: detail.PropertyName,
            MachineName: detail.MachineName,
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
            MachineCondition: detail.MachineCondition,
            MachineAge: detail.MachineAge,
            MachineEfficiency: detail.MachineEfficiency,
            MachineTechnology: detail.MachineTechnology,
            UsePurpose: detail.UsePurpose,
            MachinePart: detail.MachinePart,
            Remark: detail.Remark,
            Other: detail.Other,
            AppraiserOpinion: detail.AppraiserOpinion);
    }
}
