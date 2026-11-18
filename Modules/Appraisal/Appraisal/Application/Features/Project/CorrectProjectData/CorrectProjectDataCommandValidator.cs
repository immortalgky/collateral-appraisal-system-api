using System.Text.Json;

namespace Appraisal.Application.Features.Project.CorrectProjectData;

public class CorrectProjectDataCommandValidator : AbstractValidator<CorrectProjectDataCommand>
{
    public CorrectProjectDataCommandValidator()
    {
        RuleFor(x => x.AppraisalId).NotEmpty();

        RuleFor(x => x.Target)
            .Must(ProjectCorrectionTargets.IsKnown)
            .WithMessage(x => $"'{x.Target}' is not a correctable project part.");

        RuleFor(x => x.TargetId)
        .NotEmpty()
            .When(x => ProjectCorrectionTargets.NeedsId(x.Target))
            .WithMessage("A model or tower id is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required for every data correction.")
            .MaximumLength(4000);

        RuleFor(x => x.Import)
            .NotNull()
            .When(x => x.Target == ProjectCorrectionTargets.UnitPriceImport)
            .WithMessage("The price list file is required.");

        RuleFor(x => x.Data)
            .Must(d => d is { ValueKind: JsonValueKind.Object })
            .When(x => x.Target != ProjectCorrectionTargets.UnitPriceImport)
            .WithMessage("'data' must be the JSON object the block page would send.");
    }
}
