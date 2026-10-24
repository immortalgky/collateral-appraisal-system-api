namespace Collateral.Application.Features.BlockReappraisal.RestoreBlockReappraisal;

/// <summary>
/// Re-opens the project's due row and clears the master's not-required exclusion: the daily
/// BlockReappraisalJob skips excluded masters and prunes Pending rows it no longer finds due, so
/// an excluded master would drop off the list again on the next scan.
/// Only a Deleted (not required) row is restored; any other status is left as it is.
/// </summary>
public class RestoreBlockReappraisalCommandHandler(
    ICollateralMasterRepository repository,
    CollateralDbContext dbContext)
    : ICommandHandler<RestoreBlockReappraisalCommand, RestoreBlockReappraisalResult>
{
    public async Task<RestoreBlockReappraisalResult> Handle(
        RestoreBlockReappraisalCommand command,
        CancellationToken cancellationToken)
    {
        var dueRow = await dbContext.BlockReappraisalDue
            .FirstOrDefaultAsync(r => r.CollateralMasterId == command.CollateralMasterId, cancellationToken);
        if (dueRow is null)
            throw new NotFoundException("BlockReappraisalDue", command.CollateralMasterId);

        if (dueRow.Status != "Deleted")
            return new RestoreBlockReappraisalResult(command.CollateralMasterId);

        var master = await repository.FindByIdAsync(command.CollateralMasterId, cancellationToken);
        if (master is { ExcludedFromReappraisal: true })
            master.ClearExclusionFromReappraisal();

        dueRow.Reactivate();

        await repository.SaveChangesAsync(cancellationToken);

        return new RestoreBlockReappraisalResult(command.CollateralMasterId);
    }
}
