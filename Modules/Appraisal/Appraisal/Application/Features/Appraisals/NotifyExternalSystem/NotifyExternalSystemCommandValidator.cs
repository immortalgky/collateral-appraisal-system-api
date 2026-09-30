namespace Appraisal.Application.Features.Appraisals.NotifyExternalSystem;

public class NotifyExternalSystemCommandValidator : AbstractValidator<NotifyExternalSystemCommand>
{
    public NotifyExternalSystemCommandValidator()
    {
        RuleFor(x => x.AppraisalId).NotEmpty();

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required to notify the source system.")
            .MaximumLength(4000);
    }
}
