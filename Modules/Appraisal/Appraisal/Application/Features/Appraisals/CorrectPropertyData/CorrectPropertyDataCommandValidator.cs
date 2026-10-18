using System.Text.Json;

namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

public class CorrectPropertyDataCommandValidator : AbstractValidator<CorrectPropertyDataCommand>
{
    public CorrectPropertyDataCommandValidator()
    {
        RuleFor(x => x.AppraisalId).NotEmpty();
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.Suffix).NotEmpty();

        // The whole point of this feature is an attributable edit trail, so a correction without a
        // stated reason is not accepted.
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required for every data correction.")
            .MaximumLength(4000);

        RuleFor(x => x.Data.ValueKind)
            .Equal(JsonValueKind.Object).WithMessage("'data' must be the JSON object the real page would send.");
    }
}
