namespace Appraisal.Application.Features.Fees.UpdateFeeItem;

public class UpdateFeeItemCommandHandler(AppraisalDbContext dbContext)
    : ICommandHandler<UpdateFeeItemCommand, UpdateFeeItemResult>
{
    public async Task<UpdateFeeItemResult> Handle(UpdateFeeItemCommand command, CancellationToken cancellationToken)
    {
        var fee = await dbContext.AppraisalFees
            .Include(fi => fi.Items)
            .FirstOrDefaultAsync(f => f.Id == command.FeeId, cancellationToken);

        if (fee is null)
            throw new NotFoundException("AppraisalFee", command.FeeId);

        var item = fee.UpdateItem(command.FeeItemId, command.FeeCode, command.FeeDescription, command.FeeAmount);

        return new UpdateFeeItemResult(item.Id, item.FeeCode, item.FeeDescription, item.FeeAmount);
    }
}