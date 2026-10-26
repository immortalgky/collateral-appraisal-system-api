namespace Appraisal.Application.Features.Quotations.FinalizeQuotation;

/// <summary>
/// Runs in the MediatR pipeline BEFORE the finalize transaction, so a bad auto-assign staff list is a
/// 400 and nothing is finalized. A blank staffId is valid (it means round robin).
/// </summary>
public class FinalizeQuotationCommandValidator : AbstractValidator<FinalizeQuotationCommand>
{
    public FinalizeQuotationCommandValidator()
    {
        When(c => c.AutoAssignStaff is not null, () =>
        {
            RuleFor(c => c.AutoAssignStaff!)
                .Must(staff => staff.All(s => s is not null && s.AppraisalId != Guid.Empty))
                .WithMessage("Each autoAssign staff entry needs a non-empty appraisalId")
                .Must(staff => staff.Where(s => s is not null).Select(s => s!.AppraisalId).Distinct().Count()
                               == staff.Count(s => s is not null))
                .WithMessage("autoAssign staff must not list the same appraisal twice");
        });
    }
}
