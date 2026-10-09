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

        MachineryPropertyApplier.Apply(property, command);

        return Unit.Value;
    }
}
