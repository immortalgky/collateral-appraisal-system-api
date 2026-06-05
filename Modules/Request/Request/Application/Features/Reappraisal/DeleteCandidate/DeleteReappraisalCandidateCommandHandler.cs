namespace Request.Application.Features.Reappraisal.DeleteCandidate;

public class DeleteReappraisalCandidateCommandHandler(RequestDbContext dbContext)
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

        return new DeleteReappraisalCandidateResult(true);
    }
}
