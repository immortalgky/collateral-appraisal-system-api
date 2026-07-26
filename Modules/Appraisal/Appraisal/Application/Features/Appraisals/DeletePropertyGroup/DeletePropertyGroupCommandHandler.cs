using Appraisal.Application.Services;

namespace Appraisal.Application.Features.Appraisals.DeletePropertyGroup;

/// <summary>
/// Handler for deleting a PropertyGroup.
/// Also deletes the associated PricingAnalysis and any reference analyses hosted by its methods (DL10).
/// </summary>
public class DeletePropertyGroupCommandHandler(
    IAppraisalRepository appraisalRepository,
    PricingReferenceCleanupService cleanupService,
    AppraisalValuationSummaryService valuationSummaryService
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

        // Flush the group's PricingAnalysis deletion BEFORE recomputing the appraisal summary.
        // RecomputeAsync sums PropertyGroup PricingAnalyses with SQL, and a row in the Deleted state
        // is still live until the save runs — recomputing first would keep counting the deleted
        // group. The deletion raises no domain event, so without this the ValuationAnalyses total
        // stays stale until the next unrelated pricing edit. The intermediate save + the summary
        // upsert/outbox both commit inside TransactionalBehavior's transaction
        // (DeletePropertyGroupCommand is ITransactionalCommand). Deleting the last group lands 0.
        await appraisalRepository.SaveChangesAsync(cancellationToken);
        await valuationSummaryService.RecomputeAsync(command.AppraisalId, cancellationToken);

        return new DeletePropertyGroupResult(true);
    }
}
