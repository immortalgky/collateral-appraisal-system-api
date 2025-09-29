using FluentValidation;

namespace Collateral.Collateral.Shared.Features.UpdateCollateralEngagement;

public class UpdateCollateralEngagementCommandValidator
    : AbstractValidator<UpdateCollateralEngagementCommand>
{
    public UpdateCollateralEngagementCommandValidator()
    {
        RuleFor(x => x.CollatId)
            .NotNull()
            .WithMessage("Id is required.")
            .GreaterThan(0)
            .WithMessage("Id must be greater than 0.");
    }
}
