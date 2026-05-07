namespace Appraisal.Application.Features.Fees.UpdateConstructionInspectionFee;

public class UpdateConstructionInspectionFeeCommandHandler(AppraisalDbContext dbContext)
    : ICommandHandler<UpdateConstructionInspectionFeeCommand>
{
    public async Task<Unit> Handle(UpdateConstructionInspectionFeeCommand command, CancellationToken cancellationToken)
    {
        var fee = await dbContext.AppraisalFees.FindAsync([command.FeeId], cancellationToken);

        if (fee is null)
            throw new NotFoundException("Fee", command.FeeId);

        fee.SetConstructionInspectionFee(command.Amount);

        return Unit.Value;
    }
}
