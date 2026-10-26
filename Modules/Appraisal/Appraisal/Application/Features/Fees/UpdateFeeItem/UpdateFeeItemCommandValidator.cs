using Parameter.Contracts.Parameters;

namespace Appraisal.Application.Features.Fees.UpdateFeeItem;

public class UpdateFeeItemCommandValidator : AbstractValidator<UpdateFeeItemCommand>
{
    public UpdateFeeItemCommandValidator(IParameterLookupService parameterLookup, AppraisalDbContext dbContext) =>
        FeeItemRules.Apply(
            this, parameterLookup, x => x.FeeCode, x => x.FeeDescription, x => x.FeeAmount,
            currentCode: (command, ct) => dbContext.AppraisalFeeItems
                .AsNoTracking()
                .Where(i => i.Id == command.FeeItemId)
                .Select(i => i.FeeCode)
                .FirstOrDefaultAsync(ct));
}
