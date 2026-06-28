namespace Appraisal.Application.Features.FeeStructures.UpdateFeeStructure;

public class UpdateFeeStructureCommandValidator : AbstractValidator<UpdateFeeStructureCommand>
{
    public UpdateFeeStructureCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.BaseAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Base amount cannot be negative.");

        RuleFor(x => x.MinSellingPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Min selling price cannot be negative.");

        RuleFor(x => x.MaxSellingPrice)
            .GreaterThanOrEqualTo(x => x.MinSellingPrice)
            .When(x => x.MaxSellingPrice.HasValue)
            .WithMessage("Max selling price must be greater than or equal to min selling price.");
    }
}
