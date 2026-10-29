using FluentValidation;

namespace Appraisal.Application.Features.Appraisals.UpdateInspectionNumber;

public class UpdateInspectionNumberCommandValidator : AbstractValidator<UpdateInspectionNumberCommand>
{
    public UpdateInspectionNumberCommandValidator()
    {
        RuleFor(x => x.InspectionNumber)
            .GreaterThan(0).WithMessage("InspectionNumber must be positive.");
    }
}
