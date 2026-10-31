using Appraisal.Application.Features.Assignments.AssignmentEmails;

namespace Appraisal.Application.Features.Assignments.AssignAppraisal;

/// <summary>
/// EXTO names the company the bank engaged off-system: the admin must pick it at assignment, as nothing
/// downstream (no CompanySelectionActivity runs on that path) can choose it.
/// An EXT / EXTO assign that sends the email must carry a well-formed one; otherwise the email is not read.
/// </summary>
public class AssignAppraisalCommandValidator : AbstractValidator<AssignAppraisalCommand>
{
    public AssignAppraisalCommandValidator()
    {
        RuleFor(c => c.AssigneeCompanyId)
            .Must(id => Guid.TryParse(id, out var companyId) && companyId != Guid.Empty)
            // The handler's own predicate, so "required here" and "looked up and recorded there" cannot disagree.
            .When(c => AssignAppraisalCommandHandler.IsOfflineExternal(c.DecisionTaken))
            .WithMessage("The external company is required for an off-system (EXTO) assignment.");

        When(c => AssignAppraisalCommandHandler.SendsEmail(c), () =>
        {
            RuleFor(c => c.Email).NotNull().WithMessage("The email to the company is required to send it.");
            RuleFor(c => c.Email!).SetValidator(new AssignmentEmailInputValidator()).When(c => c.Email is not null);
        });
    }
}
