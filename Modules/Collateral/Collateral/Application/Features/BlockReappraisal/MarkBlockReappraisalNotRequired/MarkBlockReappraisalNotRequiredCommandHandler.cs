using Shared.Identity;
using Shared.Time;

namespace Collateral.Application.Features.BlockReappraisal.MarkBlockReappraisalNotRequired;

/// <summary>
/// Loads the CollateralMaster via the repository, marks it as excluded from reappraisal,
/// and consumes the corresponding BlockReappraisalDue row so it no longer appears in the
/// Pending due list.
/// </summary>
public class MarkBlockReappraisalNotRequiredCommandHandler(
    ICollateralMasterRepository repository,
    CollateralDbContext dbContext,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MarkBlockReappraisalNotRequiredCommand, MarkBlockReappraisalNotRequiredResult>
{
    public async Task<MarkBlockReappraisalNotRequiredResult> Handle(
        MarkBlockReappraisalNotRequiredCommand command,
        CancellationToken cancellationToken)
    {
        var master = await repository.FindByIdAsync(command.CollateralMasterId, cancellationToken);
        if (master is null)
            throw new NotFoundException("CollateralMaster", command.CollateralMasterId);

        var by = currentUser.Username ?? currentUser.UserId?.ToString() ?? "unknown";
        master.MarkExcludedFromReappraisal(by, dateTimeProvider.UtcNow);

        // Move the pending due row to Deleted so it leaves the Pending list for the not-required tab.
        var dueRow = await dbContext.BlockReappraisalDue
            .FirstOrDefaultAsync(r => r.CollateralMasterId == command.CollateralMasterId, cancellationToken);

        dueRow?.MarkNotRequired(command.Remark);

        await repository.SaveChangesAsync(cancellationToken);

        return new MarkBlockReappraisalNotRequiredResult(command.CollateralMasterId);
    }
}
