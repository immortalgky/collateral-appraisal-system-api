using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UpdateVehicleProperty;

/// <summary>
/// Handler for updating a vehicle property detail
/// </summary>
public class UpdateVehiclePropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateVehiclePropertyCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateVehiclePropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(command.PropertyId)
            ?? throw new PropertyNotFoundException(command.PropertyId);

        VehiclePropertyApplier.Apply(property, command);

        return MediatR.Unit.Value;
    }
}
