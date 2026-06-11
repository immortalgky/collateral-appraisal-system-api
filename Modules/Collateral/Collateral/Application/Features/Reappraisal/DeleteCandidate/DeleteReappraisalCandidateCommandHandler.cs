namespace Collateral.Application.Features.Reappraisal.DeleteCandidate;

public class DeleteReappraisalCandidateCommandHandler(CollateralDbContext dbContext)
    : ICommandHandler<DeleteReappraisalCandidateCommand, DeleteReappraisalCandidateResult>
{
    public async Task<DeleteReappraisalCandidateResult> Handle(
        DeleteReappraisalCandidateCommand command,
        CancellationToken cancellationToken)
    {
        var candidate = await dbContext.ReappraisalCandidates
            .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);

        if (candidate is null)
            return new DeleteReappraisalCandidateResult(false);

        candidate.MarkDeleted();
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeleteReappraisalCandidateResult(true);
    }
}
