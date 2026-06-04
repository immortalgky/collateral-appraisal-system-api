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

        // Consume the pending due row so it immediately leaves the list.
        var dueRow = await dbContext.BlockReappraisalDue
            .FirstOrDefaultAsync(r => r.CollateralMasterId == command.CollateralMasterId, cancellationToken);

        dueRow?.MarkConsumed();

        await repository.SaveChangesAsync(cancellationToken);

        return new MarkBlockReappraisalNotRequiredResult(command.CollateralMasterId);
    }
}
