using Appraisal.Application.Features.Appraisals;
using FluentValidation;

namespace Appraisal.Application.Features.Project.CreateProjectModel;

public class CreateProjectModelCommandValidator : AbstractValidator<CreateProjectModelCommand>
{
    public CreateProjectModelCommandValidator(ISender mediator)
    {
        // The block write paths accepted any string until now, and an unrecognized one costs the
        // model its coverage amount on every unit price the project calculates — silently, since
        // Project.LookupRate returns null and the caller falls back to a manual CoverageAmount.
        RuleFor(x => x.FireInsuranceCode)
            .MustAsync((code, cancellationToken) =>
                CondoFireInsuranceCalculator.IsKnownRateCodeAsync(mediator, code!, cancellationToken))
            .When(x => !string.IsNullOrEmpty(x.FireInsuranceCode))
            .WithMessage("Fire insurance condition is not a recognized fire-insurance rate.");
    }
}
