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
                     ?? throw new InvalidOperationException(
                         $"Machinery detail not found for property {query.PropertyId}");

        // 5. Map to result
        return new GetMachineryPropertyResult(
            property.Id,
            property.AppraisalId,
            property.SequenceNumber,
            property.PropertyType.ToString(),
            property.Description,
            detail.Id,
            // Resolved here rather than in each caller: the 360 slide-over renders this response
            // as it comes, so a machine whose name is still in the old MachineName column would
            // otherwise show as nameless there. MachineName is returned untouched below for
            // anything that wants the raw column.
            string.IsNullOrWhiteSpace(detail.PropertyName) ? detail.MachineName : detail.PropertyName,
            detail.MachineName,
            detail.EngineNo,
            detail.ChassisNo,
            detail.RegistrationNumber,
            detail.SerialNo,
            detail.RegistrationStatus,
            detail.InstallationStatus,
            detail.MachineType,
            detail.InvoiceNumber,
            detail.IsPriceCertified,
            detail.Brand,
            detail.Model,
            detail.Series,
            detail.YearOfManufacture,
            detail.Manufacturer,
            detail.PurchaseDate,
            detail.PurchasePrice,
            detail.Capacity,
            detail.Quantity,
            detail.MachineDimensions,
            detail.Width,
            detail.Length,
            detail.Height,
            detail.EnergyUse,
            detail.EnergyUseRemark,
            detail.OwnerName,
            detail.IsOwnerVerified,
            detail.IsOperational,
            detail.Location,
            detail.ConditionUse,
            detail.MachineCondition,
            detail.MachineAge,
            detail.MachineEfficiency,
            detail.MachineTechnology,
            detail.UsagePurpose,
            detail.MachineParts,
            detail.ReplacementValue,
            detail.ConditionValue,
            detail.Remark,
            detail.Other,
            detail.AppraiserOpinion);
    }
}