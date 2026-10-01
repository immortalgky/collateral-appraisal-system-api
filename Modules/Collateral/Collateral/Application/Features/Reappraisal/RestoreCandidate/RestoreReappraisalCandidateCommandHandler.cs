using Collateral.CollateralMasters.Reappraisal;

namespace Collateral.Application.Features.Reappraisal.RestoreCandidate;

public class RestoreReappraisalCandidateCommandHandler(CollateralDbContext dbContext)
    : ICommandHandler<RestoreReappraisalCandidateCommand, RestoreReappraisalCandidateResult>
{
    public async Task<RestoreReappraisalCandidateResult> Handle(
        RestoreReappraisalCandidateCommand command,
        CancellationToken cancellationToken)
    {
        var candidate = await dbContext.ReappraisalCandidates
            .FirstOrDefaultAsync(c => c.Id == command.Id
                                      && c.Status == ReappraisalCandidateStatus.Deleted,
                cancellationToken);

        if (candidate is null)
            return new RestoreReappraisalCandidateResult(false);

        candidate.MarkPending();
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RestoreReappraisalCandidateResult(true);
    }
}
