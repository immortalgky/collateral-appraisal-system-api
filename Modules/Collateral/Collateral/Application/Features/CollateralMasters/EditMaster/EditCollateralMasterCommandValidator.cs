using FluentValidation;

namespace Collateral.Application.Features.CollateralMasters.EditMaster;

public class EditCollateralMasterCommandValidator : AbstractValidator<EditCollateralMasterCommand>
{
    public EditCollateralMasterCommandValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Reason is required for admin edits.");
    }
}
