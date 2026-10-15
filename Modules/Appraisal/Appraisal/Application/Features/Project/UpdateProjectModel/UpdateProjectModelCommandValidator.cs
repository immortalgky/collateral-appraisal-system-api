using Appraisal.Application.Features.Appraisals;
using FluentValidation;

namespace Appraisal.Application.Features.Project.UpdateProjectModel;

public class UpdateProjectModelCommandValidator : AbstractValidator<UpdateProjectModelCommand>
{
    public UpdateProjectModelCommandValidator(ISender mediator)
    {
        // Mirrors CreateProjectModelCommandValidator — see the note there.
        RuleFor(x => x.FireInsuranceCode)
            .MustAsync((code, cancellationToken) =>
                CondoFireInsuranceCalculator.IsKnownRateCodeAsync(mediator, code!, cancellationToken))
            .When(x => !string.IsNullOrEmpty(x.FireInsuranceCode))
            .WithMessage("Fire insurance condition is not a recognized fire-insurance rate.");
    }
}
