namespace Appraisal.Application.Features.Fees.RejectFeeItem;

public class RejectFeeItemCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext,
    ICurrentUserService currentUser)
    : ICommandHandler<RejectFeeItemCommand>
{
    public async Task<Unit> Handle(
        RejectFeeItemCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        var fee = await dbContext.AppraisalFees
                      .FirstOrDefaultAsync(f => f.Id == command.FeeId, cancellationToken)
                  ?? throw new NotFoundException("AppraisalFee", command.FeeId);

        // Verify the fee's assignment belongs to this appraisal
        var assignmentBelongs = appraisal.Assignments.Any(a => a.Id == fee.AssignmentId);
        if (!assignmentBelongs)
            throw new InvalidOperationException("Fee does not belong to this appraisal.");

        var item = await dbContext.AppraisalFeeItems
                       .FirstOrDefaultAsync(i => i.Id == command.ItemId && i.AppraisalFeeId == command.FeeId,
                           cancellationToken)
                   ?? throw new NotFoundException("AppraisalFeeItem", command.ItemId);

        // Stamp the authenticated caller's bank code — never trust a client-supplied actor.
        item.Reject(currentUser.UserCode ?? "system", command.Reason);

        return Unit.Value;
    }
}
