namespace Appraisal.Application.Features.FeeStructures.CreateFeeStructure;

public class CreateFeeStructureCommandValidator : AbstractValidator<CreateFeeStructureCommand>
{
    public CreateFeeStructureCommandValidator()
    {
        RuleFor(x => x.FeeCode)
            .NotEmpty().WithMessage("Fee code is required.")
            .MaximumLength(20);

        RuleFor(x => x.BaseAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Base amount cannot be negative.");

        RuleFor(x => x.MinSellingPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Min selling price cannot be negative.");

        // Null/blank = generic ladder; anything else must be a recognised appraisal type, otherwise
        // the tier would be scoped to a value no appraisal can ever carry.
        RuleFor(x => x.AppraisalType)
            .Must(AppraisalTypes.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.AppraisalType))
            .WithMessage($"Appraisal type must be one of: {string.Join(", ", AppraisalTypes.ValidValues)}.");

        RuleFor(x => x.MaxSellingPrice)
            .GreaterThanOrEqualTo(x => x.MinSellingPrice)
            .When(x => x.MaxSellingPrice.HasValue)
            .WithMessage("Max selling price must be greater than or equal to min selling price.");
    }
}
