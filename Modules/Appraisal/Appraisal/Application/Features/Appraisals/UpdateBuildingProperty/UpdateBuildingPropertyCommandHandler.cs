using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;
using Appraisal.Application.Services;

namespace Appraisal.Application.Features.Appraisals.UpdateBuildingProperty;

/// <summary>
/// Handler for updating a building property detail
/// </summary>
public class UpdateBuildingPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalValuationSummaryService valuationSummaryService
) : ICommandHandler<UpdateBuildingPropertyCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateBuildingPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);
        
        var property = appraisal.GetProperty(command.PropertyId)
            ?? throw new PropertyNotFoundException(command.PropertyId);

        BuildingPropertyApplier.Apply(property, command);

        await valuationSummaryService.RecomputeAsync(command.AppraisalId, cancellationToken);

        return MediatR.Unit.Value;
    }
}
