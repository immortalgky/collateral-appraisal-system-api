using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.Shared;

namespace Appraisal.Application.Features.Appraisals.UpdateLandProperty;

/// <summary>
/// Handler for updating a land property detail
/// </summary>
public class UpdateLandPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateLandPropertyCommand>
{
    public async Task<Unit> Handle(
        UpdateLandPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.GetProperty(command.PropertyId)
                       ?? throw new PropertyNotFoundException(command.PropertyId);

        LandPropertyApplier.Apply(property, command);

        return Unit.Value;
    }
}
