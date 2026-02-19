namespace Appraisal.Application.Features.Fees.UpdatePayment;

public class UpdatePaymentCommandHandler(AppraisalDbContext dbContext)
    : ICommandHandler<UpdatePaymentCommand, UpdatePaymentResult>
{
    public async Task<UpdatePaymentResult> Handle(UpdatePaymentCommand command, CancellationToken cancellationToken)
    {
        var fee = await dbContext.AppraisalFees
            .Include(f => f.PaymentHistory)
            .FirstOrDefaultAsync(f => f.Id == command.FeeId, cancellationToken);
        if (fee is null)
            throw new NotFoundException("Fee", command.FeeId);

        fee.UpdatePayment(command.PaymentId, command.PaymentAmount, command.PaymentDate);

        return new UpdatePaymentResult(command.PaymentId, command.PaymentAmount, command.PaymentDate);
    }
}