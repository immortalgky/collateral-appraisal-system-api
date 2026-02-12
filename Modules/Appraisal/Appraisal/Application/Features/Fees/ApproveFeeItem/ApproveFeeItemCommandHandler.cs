namespace Appraisal.Application.Features.Fees.ApproveFeeItem;

public class ApproveFeeItemCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext)
    : ICommandHandler<ApproveFeeItemCommand>
{
    public async Task<Unit> Handle(
        ApproveFeeItemCommand command,
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

        item.Approve(command.ApprovedBy);

        return Unit.Value;
    }
}
