using Collateral.CollateralMasters.Reappraisal;

namespace Collateral.Application.Features.Reappraisal.DeleteCandidate;

public class DeleteReappraisalCandidateCommandHandler(CollateralDbContext dbContext)
    : ICommandHandler<DeleteReappraisalCandidateCommand, DeleteReappraisalCandidateResult>
{
    public async Task<DeleteReappraisalCandidateResult> Handle(
        DeleteReappraisalCandidateCommand command,
        CancellationToken cancellationToken)
    {
        var candidate = await dbContext.ReappraisalCandidates
            // A reviewed (Consumed) book is not "not reviewing this round": marking it Deleted would let
            // later files refresh it and a restore put it back on the to-do list.
            .FirstOrDefaultAsync(c => c.Id == command.Id
                                      && c.Status != ReappraisalCandidateStatus.Consumed, cancellationToken);

        if (candidate is null)
            return new DeleteReappraisalCandidateResult(false);

        candidate.MarkDeleted();
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeleteReappraisalCandidateResult(true);
    }
}
