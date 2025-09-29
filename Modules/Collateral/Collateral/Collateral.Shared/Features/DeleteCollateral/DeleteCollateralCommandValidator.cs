using FluentValidation;

namespace Collateral.Collateral.Shared.Features.DeleteCollateral;

public class DeleteCollateralCommandValidator : AbstractValidator<DeleteCollateralCommand>
{
    public DeleteCollateralCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotNull()
            .WithMessage("Id is required.")
            .GreaterThan(0)
            .WithMessage("Id must be greater than 0.");
    }
}
