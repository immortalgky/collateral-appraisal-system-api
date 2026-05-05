using FluentValidation;

namespace Collateral.Application.Features.CollateralMasters.RestoreMaster;

public class RestoreCollateralMasterCommandValidator : AbstractValidator<RestoreCollateralMasterCommand>
{
    public RestoreCollateralMasterCommandValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Reason is required when restoring a collateral master.");
    }
}
