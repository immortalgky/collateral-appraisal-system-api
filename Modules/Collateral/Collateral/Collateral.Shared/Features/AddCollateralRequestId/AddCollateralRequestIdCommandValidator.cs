using FluentValidation;

namespace Collateral.Collateral.Shared.Features.AddCollateralRequestId;

public class AddCollateralRequestIdCommandValidator
    : AbstractValidator<AddCollateralRequestIdCommand>
{
    public AddCollateralRequestIdCommandValidator()
    {
        RuleFor(x => x.CollatId)
            .NotNull()
            .WithMessage("Id is required.")
            .GreaterThan(0)
            .WithMessage("Id must be greater than 0.");

        RuleFor(x => x.ReqId)
            .NotNull()
            .WithMessage("Id is required.")
            .GreaterThan(0)
            .WithMessage("Id must be greater than 0.");
    }
}
