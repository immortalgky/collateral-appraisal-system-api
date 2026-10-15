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

        VesselPropertyApplier.Apply(property, command);

        return MediatR.Unit.Value;
    }
}
