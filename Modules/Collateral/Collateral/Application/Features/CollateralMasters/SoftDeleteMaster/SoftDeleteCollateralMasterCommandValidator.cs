using FluentValidation;

namespace Collateral.Application.Features.CollateralMasters.SoftDeleteMaster;

public class SoftDeleteCollateralMasterCommandValidator : AbstractValidator<SoftDeleteCollateralMasterCommand>
{
    public SoftDeleteCollateralMasterCommandValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Reason is required when soft-deleting a collateral master.");
    }
}
