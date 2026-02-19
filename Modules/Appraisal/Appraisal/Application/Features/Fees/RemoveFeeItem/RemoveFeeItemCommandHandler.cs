namespace Appraisal.Application.Features.Fees.RemoveFeeItem;

public class RemoveFeeItemCommandHandler(AppraisalDbContext dbContext) : ICommandHandler<RemoveFeeItemCommand>
{
    public async Task<Unit> Handle(RemoveFeeItemCommand command, CancellationToken cancellationToken)
    {
        var fee = await dbContext.AppraisalFees
            .Include(fi => fi.Items)
            .FirstOrDefaultAsync(f => f.Id == command.FeeId, cancellationToken);
        if (fee is null)
            throw new NotFoundException("Fee", command.FeeId);

        fee.RemoveItem(command.FeeItemId);

        return Unit.Value;
    }
}