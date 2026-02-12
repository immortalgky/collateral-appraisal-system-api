namespace Appraisal.Application.Features.Fees.RecordPayment;

public class RecordPaymentCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext)
    : ICommandHandler<RecordPaymentCommand>
{
    public async Task<Unit> Handle(
        RecordPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        var fee = await dbContext.AppraisalFees
                      .Include(f => f.PaymentHistory)
                      .FirstOrDefaultAsync(f => f.Id == command.FeeId, cancellationToken)
                  ?? throw new NotFoundException("AppraisalFee", command.FeeId);

        // Verify the fee's assignment belongs to this appraisal
        var assignmentBelongs = appraisal.Assignments.Any(a => a.Id == fee.AssignmentId);
        if (!assignmentBelongs)
            throw new InvalidOperationException("Fee does not belong to this appraisal.");

        fee.RecordPayment(
            command.PaymentAmount,
            command.PaymentDate,
            command.PaymentMethod,
            command.PaymentReference,
            command.Remarks);

        return Unit.Value;
    }
}
