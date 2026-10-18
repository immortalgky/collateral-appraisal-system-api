using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Application.Services;

namespace Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;

/// <summary>
/// Handler for updating a land and building property detail
/// </summary>
public class UpdateLandAndBuildingPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalValuationSummaryService valuationSummaryService
) : ICommandHandler<UpdateLandAndBuildingPropertyCommand>
{
    public async Task<Unit> Handle(
        UpdateLandAndBuildingPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(command.PropertyId)
                       ?? throw new PropertyNotFoundException(command.PropertyId);

        LandAndBuildingPropertyApplier.Apply(property, command);

        await valuationSummaryService.RecomputeAsync(command.AppraisalId, cancellationToken);

        return Unit.Value;
    }
}
