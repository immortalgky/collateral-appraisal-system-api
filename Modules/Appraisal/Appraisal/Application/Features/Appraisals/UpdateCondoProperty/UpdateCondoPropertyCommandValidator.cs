using Appraisal.Application.Features.Appraisals;
using FluentValidation;

namespace Appraisal.Application.Features.Appraisals.UpdateCondoProperty;

public class UpdateCondoPropertyCommandValidator : AbstractValidator<UpdateCondoPropertyCommand>
{
    public UpdateCondoPropertyCommandValidator(ISender mediator)
    {
        // Reject a condition that isn't one of the seeded Condo fire-insurance rates — otherwise
        // BuildingInsurancePrice silently derives to null with no feedback to the caller.
        RuleFor(x => x.FireInsuranceCondition)
            .MustAsync((condition, cancellationToken) =>
                CondoFireInsuranceCalculator.IsKnownConditionAsync(mediator, condition!, cancellationToken))
            .When(x => !string.IsNullOrEmpty(x.FireInsuranceCondition))
            .WithMessage("Fire insurance condition is not a recognized Condo condition.");
    }
}
