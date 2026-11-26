namespace Appraisal.Application.Features.Appraisals.SuspendAppraisal;

public class SuspendAppraisalCommandValidator : AbstractValidator<SuspendAppraisalCommand>
{
    public SuspendAppraisalCommandValidator()
    {
        RuleFor(x => x.AppraisalId).NotEmpty();

        // Required for both actions. 4000 is the column size, so an over-long reason returns 400, not a SQL error.
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(4000);
    }
}
