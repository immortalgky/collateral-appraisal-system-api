namespace Appraisal.Application.Features.CommitteeVoting.AssignCommittee;

public class AssignCommitteeCommandHandler(
    AppraisalDbContext dbContext,
    ICommitteeRepository committeeRepository,
    ISqlConnectionFactory connectionFactory
) : ICommandHandler<AssignCommitteeCommand, AssignCommitteeResult>
{
    public async Task<AssignCommitteeResult> Handle(
        AssignCommitteeCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Verify appraisal exists
        var appraisalExists = await dbContext.Appraisals
            .AnyAsync(a => a.Id == command.AppraisalId, cancellationToken);
        if (!appraisalExists)
            throw new NotFoundException("Appraisal", command.AppraisalId);

        // 2. Check no existing pending committee review
        var existingReview = await dbContext.AppraisalReviews
            .FirstOrDefaultAsync(r => r.AppraisalId == command.AppraisalId
                                      && r.ReviewLevel == "Committee"
                                      && r.Status.Code == ReviewStatus.Pending.Code, cancellationToken);
        if (existingReview != null)
            throw new InvalidOperationException("A pending committee review already exists for this appraisal.");

        // 3. Calculate total appraisal value
        const string valueSql = """
            SELECT ISNULL(SUM(pa.FinalAppraisedValue), 0)
            FROM appraisal.PricingAnalysis pa
            JOIN appraisal.PropertyGroups pg ON pg.Id = pa.PropertyGroupId
            WHERE pg.AppraisalId = @AppraisalId
            """;

        var totalValue = await connectionFactory.ExecuteScalarAsync<decimal>(valueSql, new { command.AppraisalId });

        // 4. Find committee by threshold
        var committee = await committeeRepository.GetCommitteeForValueAsync(totalValue, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No committee configured for appraisal value {totalValue:N2}. Please configure committee thresholds.");

        // 5. Get next review sequence
        var maxSequence = await dbContext.AppraisalReviews
            .Where(r => r.AppraisalId == command.AppraisalId)
            .MaxAsync(r => (int?)r.ReviewSequence, cancellationToken) ?? 0;

        // 6. Create committee review
        var review = AppraisalReview.Create(
            command.AppraisalId,
            "Committee",
            maxSequence + 1);

        review.SetCommittee(committee.Id);

        dbContext.AppraisalReviews.Add(review);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AssignCommitteeResult(review.Id, committee.CommitteeName);
    }
}
