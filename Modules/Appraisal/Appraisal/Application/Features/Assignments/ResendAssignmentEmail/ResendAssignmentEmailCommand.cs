using Appraisal.Application.Features.Assignments.AssignmentEmails;

namespace Appraisal.Application.Features.Assignments.ResendAssignmentEmail;

/// <summary>
/// ส่งอีกครั้ง / ส่งอีเมล from the read-only slip (#623): the email the admin edited, pre-filled from the latest one
/// (or from the preview when the case was assigned without sending).
/// </summary>
public record ResendAssignmentEmailCommand(Guid AppraisalId, AssignmentEmailInput Email) : ICommand<ResendAssignmentEmailResult>;

public record ResendAssignmentEmailResult(Guid EmailId);

public class ResendAssignmentEmailCommandValidator : AbstractValidator<ResendAssignmentEmailCommand>
{
    public ResendAssignmentEmailCommandValidator()
    {
        RuleFor(c => c.Email).NotNull().WithMessage("The email to the company is required to send it.");
        RuleFor(c => c.Email).SetValidator(new AssignmentEmailInputValidator()).When(c => c.Email is not null);
    }
}
