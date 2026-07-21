using Appraisal.Application.Features.Appraisals;
using FluentValidation;

namespace Appraisal.Application.Features.Appraisals.CreateCondoProperty;

public class CreateCondoPropertyCommandValidator : AbstractValidator<CreateCondoPropertyCommand>
{
    public CreateCondoPropertyCommandValidator(ISender mediator)
    {
        // A property must be created within a group — prevents orphaned (groupless) properties.
        // NotEmpty on a Guid? rejects both null and Guid.Empty.
        RuleFor(x => x.GroupId)
            .NotEmpty().WithMessage("A property must be created within a group. groupId is required.");

        // Reject a condition that isn't one of the seeded Condo fire-insurance rates — otherwise
        // BuildingInsurancePrice silently derives to null with no feedback to the caller.
        RuleFor(x => x.FireInsuranceCondition)
            .MustAsync((condition, cancellationToken) =>
                CondoFireInsuranceCalculator.IsKnownConditionAsync(mediator, condition!, cancellationToken))
            .When(x => !string.IsNullOrEmpty(x.FireInsuranceCondition))
            .WithMessage("Fire insurance condition is not a recognized Condo condition.");
    }
}
