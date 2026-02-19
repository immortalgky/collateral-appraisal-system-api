namespace Appraisal.Application.Features.Fees.UpdateFee;

public class UpdateFeeCommandHandler(AppraisalDbContext dbContext) : ICommandHandler<UpdateFeeCommand>
{
    public async Task<Unit> Handle(UpdateFeeCommand command, CancellationToken cancellationToken)
    {
        var fee = await dbContext.AppraisalFees.FindAsync([command.FeeId], cancellationToken);

        if (fee is null)
            throw new NotFoundException("Fee", command.FeeId);

        fee.SetFeePaymentType(command.FeePaymentType);

        return Unit.Value;
    }
}