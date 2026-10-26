namespace Appraisal.Application.Features.Fees.UpdateFee;

public class UpdateFeeCommandHandler(AppraisalDbContext dbContext) : ICommandHandler<UpdateFeeCommand>
{
    public async Task<Unit> Handle(UpdateFeeCommand command, CancellationToken cancellationToken)
    {
        var fee = await dbContext.AppraisalFees.FindAsync([command.FeeId], cancellationToken);

        if (fee is null)
            throw new NotFoundException("Fee", command.FeeId);

        if (command.BankAbsorbAmount < 0)
            throw new BadRequestException(
                "The bank absorb amount cannot be negative.", null, "BankAbsorbNegative");

        if (await FeeAdminStage.IsAdminStageAsync(dbContext, fee.AssignmentId, cancellationToken))
        {
            // The admin is still building the fee, so its total is only partial. Keep what was asked for (the page
            // re-sends the stored figure on every save); it is capped at the final total when assignment applies it.
            fee.RestoreRequestedBankAbsorb(command.BankAbsorbAmount);
        }
        else
        {
            // A billable total exists: the absorb must fit within it. Answered here, so it is a 400 with a code
            // rather than the aggregate's guard escaping as a 500.
            if (fee.TotalFeeAfterVAT > 0 && command.BankAbsorbAmount > fee.TotalFeeAfterVAT)
                throw new BadRequestException(
                    $"The bank absorb amount cannot exceed the fee total ({fee.TotalFeeAfterVAT:F2}).",
                    null, "BankAbsorbExceedsTotal");

            fee.SetBankAbsorb(command.BankAbsorbAmount);
        }

        fee.SetFeePaymentType(command.FeePaymentType);

        return Unit.Value;
    }
}
