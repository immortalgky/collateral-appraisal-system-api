namespace Appraisal.Application.Features.Fees.CreateAppraisalFee;

public class CreateAppraisalFeeCommandHandler(
    IAppraisalRepository appraisalRepository,
    AppraisalDbContext dbContext)
    : ICommandHandler<CreateAppraisalFeeCommand, CreateAppraisalFeeResult>
{
    public async Task<CreateAppraisalFeeResult> Handle(
        CreateAppraisalFeeCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        _ = appraisal.Assignments.FirstOrDefault(a => a.Id == command.AssignmentId)
            ?? throw new NotFoundException("Assignment", command.AssignmentId);

        // Load active fee structures
        var feeStructures = await dbContext.FeeStructures
            .Where(fs => fs.IsActive)
            .ToListAsync(cancellationToken);

        var fee = AppraisalFee.Create(command.AssignmentId);

        // Add fee items from each active fee structure
        foreach (var structure in feeStructures)
        {
            fee.AddItem(structure.FeeCode, structure.FeeName, structure.BaseAmount);
        }

        if (command.BankAbsorbAmount.HasValue)
        {
            fee.SetBankAbsorb(command.BankAbsorbAmount.Value);
        }

        dbContext.AppraisalFees.Add(fee);

        return new CreateAppraisalFeeResult(fee.Id);
    }
}
