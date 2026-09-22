using Appraisal.Application.Features.Appraisals;
using FluentValidation;

namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementCondoProperty;

public class CreateLeaseAgreementCondoPropertyCommandValidator : AbstractValidator<CreateLeaseAgreementCondoPropertyCommand>
{
    public CreateLeaseAgreementCondoPropertyCommandValidator(ISender mediator)
    {
        // A property must be created within a group — prevents orphaned (groupless) properties.
        // NotEmpty on a Guid? rejects both null and Guid.Empty.
        RuleFor(x => x.GroupId)
            .NotEmpty().WithMessage("A property must be created within a group. groupId is required.");

        // Same rule the plain condo command carries: this path writes the same CondoAppraisalDetail
        // and derives the same BuildingInsurancePrice, which silently lands null on an unknown value.
        RuleFor(x => x.FireInsuranceCode)
            .MustAsync((condition, cancellationToken) =>
                CondoFireInsuranceCalculator.IsKnownConditionAsync(mediator, condition!, cancellationToken))
            .When(x => !string.IsNullOrEmpty(x.FireInsuranceCode))
            .WithMessage("Fire insurance condition is not a recognized Condo condition.");
    }
}
