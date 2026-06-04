using Appraisal.Application.Services;

namespace Appraisal.Application.Features.Appraisals.DeletePropertyGroup;

/// <summary>
/// Handler for deleting a PropertyGroup.
/// Also deletes the associated PricingAnalysis and any reference analyses hosted by its methods (DL10).
/// </summary>
public class DeletePropertyGroupCommandHandler(
    IAppraisalRepository appraisalRepository,
    PricingReferenceCleanupService cleanupService
) : ICommandHandler<DeletePropertyGroupCommand, DeletePropertyGroupResult>
{
    public async Task<DeletePropertyGroupResult> Handle(
        DeletePropertyGroupCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(command.AppraisalId, cancellationToken)
                        ?? throw new InvalidOperationException($"Appraisal {command.AppraisalId} not found");

        // Active cleanup: delete subject PA + any reference PAs hosted by its methods (DL10).
        // Must run before DeleteGroup because the group's Id is needed to find the PA.
        await cleanupService.CleanupForPropertyGroupAsync(command.GroupId, cancellationToken);

        appraisal.DeleteGroup(command.GroupId);

        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return new DeletePropertyGroupResult(true);
    }
}
