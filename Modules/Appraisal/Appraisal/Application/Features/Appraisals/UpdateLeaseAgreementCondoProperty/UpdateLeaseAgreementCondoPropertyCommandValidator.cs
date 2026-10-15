using Appraisal.Application.Features.Appraisals;
using FluentValidation;

namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementCondoProperty;

public class UpdateLeaseAgreementCondoPropertyCommandValidator
    : AbstractValidator<UpdateLeaseAgreementCondoPropertyCommand>
{
    public UpdateLeaseAgreementCondoPropertyCommandValidator(ISender mediator)
    {
        // This path writes the same CondoAppraisalDetail and derives the same BuildingInsurancePrice
        // as UpdateCondoProperty, which has carried this rule all along. Without it an unrecognized
        // value lands the coverage amount on null with nothing said to the caller.
        // No GroupId rule here: an update targets a property that already belongs to a group.
        RuleFor(x => x.FireInsuranceCode)
            .MustAsync((condition, cancellationToken) =>
                CondoFireInsuranceCalculator.IsKnownConditionAsync(mediator, condition!, cancellationToken))
            .When(x => !string.IsNullOrEmpty(x.FireInsuranceCode))
            .WithMessage("Fire insurance condition is not a recognized Condo condition.");
    }
}
