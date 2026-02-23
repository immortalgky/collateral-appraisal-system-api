namespace Appraisal.Application.Features.Fees.RemovePayment;

public class RemovePaymentCommandHandler(AppraisalDbContext dbContext) : ICommandHandler<RemovePaymentCommand>
{
    public async Task<Unit> Handle(RemovePaymentCommand command, CancellationToken cancellationToken)
    {
        var fee = await dbContext.AppraisalFees
            .Include(f => f.PaymentHistory)
            .FirstOrDefaultAsync(f => f.Id == command.FeeId, cancellationToken);
        if (fee is null)
            throw new NotFoundException("Fee", command.FeeId);

        fee.RemovePayment(command.PaymentId);

        return Unit.Value;
    }
}