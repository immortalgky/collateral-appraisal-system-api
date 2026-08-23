namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

public class CorrectPropertyDataCommandValidator : AbstractValidator<CorrectPropertyDataCommand>
{
    public CorrectPropertyDataCommandValidator()
    {
        RuleFor(x => x.AppraisalId).NotEmpty();
        RuleFor(x => x.PropertyId).NotEmpty();

        // The whole point of this feature is an attributable edit trail, so a correction without a
        // stated reason is not accepted.
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required for every data correction.")
            .MaximumLength(4000);

        RuleFor(x => x.Data).NotNull();

        // TitleId identifies which existing title row to correct; a blank one would silently match
        // nothing.
        RuleForEach(x => x.Data.LandTitles)
            .Must(t => t.TitleId != Guid.Empty)
            .When(x => x.Data?.LandTitles is not null)
            .WithMessage("Each land title correction must carry the TitleId it applies to.");
    }
}
