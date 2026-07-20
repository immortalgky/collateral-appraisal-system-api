namespace Appraisal.Application.Features.DecisionSummary.UpdateForceSaleRate;

public class UpdateForceSaleRateCommandValidator : AbstractValidator<UpdateForceSaleRateCommand>
{
    public UpdateForceSaleRateCommandValidator()
    {
        // ForceSellingRateOverride is a percent (70.00 = 70%), matching ValuationAnalysis.ForceSaleRate's
        // decimal(5,2) column — null is valid and means "clear the override, use the resolved default".
        RuleFor(x => x.ForceSellingRateOverride)
            .GreaterThan(0).WithMessage("ForceSellingRateOverride must be greater than 0.")
            .LessThanOrEqualTo(100).WithMessage("ForceSellingRateOverride cannot exceed 100.")
            .Must(rate => decimal.Round(rate!.Value, 2) == rate.Value)
            .WithMessage("ForceSellingRateOverride can have at most 2 decimal places.")
            .When(x => x.ForceSellingRateOverride.HasValue);
    }
}
