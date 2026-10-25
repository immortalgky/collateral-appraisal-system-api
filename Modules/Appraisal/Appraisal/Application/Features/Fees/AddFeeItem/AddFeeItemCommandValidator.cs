using Parameter.Contracts.Parameters;

namespace Appraisal.Application.Features.Fees.AddFeeItem;

public class AddFeeItemCommandValidator : AbstractValidator<AddFeeItemCommand>
{
    public AddFeeItemCommandValidator(IParameterLookupService parameterLookup) =>
        FeeItemRules.Apply(this, parameterLookup, x => x.FeeCode, x => x.FeeDescription, x => x.FeeAmount);
}
