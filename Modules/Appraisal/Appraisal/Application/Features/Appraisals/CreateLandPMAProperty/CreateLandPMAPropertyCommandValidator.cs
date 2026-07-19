using FluentValidation;

namespace Appraisal.Application.Features.Appraisals.CreateLandPMAProperty;

public class CreateLandPMAPropertyCommandValidator : AbstractValidator<CreateLandPMAPropertyCommand>
{
    public CreateLandPMAPropertyCommandValidator()
    {
        // A property must be created within a group — prevents orphaned (groupless) properties.
        // NotEmpty on a Guid? rejects both null and Guid.Empty.
        RuleFor(x => x.GroupId)
            .NotEmpty().WithMessage("A property must be created within a group. groupId is required.");
    }
}
