namespace Appraisal.Application.Features.Fees.AddFeeItem;

public class AddFeeItemCommandHandler(AppraisalDbContext dbContext)
    : ICommandHandler<AddFeeItemCommand, AddFeeItemResult>
{
    public async Task<AddFeeItemResult> Handle(
        AddFeeItemCommand command,
        CancellationToken cancellationToken)
    {
        var fee = await dbContext.AppraisalFees
            .Include(f => f.Items)
            .FirstOrDefaultAsync(f => f.Id == command.FeeId, cancellationToken)
            ?? throw new NotFoundException("AppraisalFee", command.FeeId);

        var item = fee.AddItem(command.FeeCode, command.FeeDescription, command.FeeAmount);

        return new AddFeeItemResult(item.Id);
    }
}
