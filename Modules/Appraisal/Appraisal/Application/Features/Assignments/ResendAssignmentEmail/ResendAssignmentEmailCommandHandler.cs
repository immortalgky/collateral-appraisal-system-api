using Appraisal.Application.Features.Assignments.AssignmentEmails;
using Appraisal.Infrastructure;

namespace Appraisal.Application.Features.Assignments.ResendAssignmentEmail;

/// <summary>
/// Sends the email to the company of the case's current assignment again: a new row (IsResend) queued and requested in
/// one save, through the same pipeline as the first send. Allowed until the case is closed, and never while the
/// assignment's own email still waits for its handover (the company would get it twice).
/// </summary>
public class ResendAssignmentEmailCommandHandler(
    ICurrentUserService currentUser,
    IAssignmentEmailCaseReader caseReader,
    IAssignmentEmailRepository emails,
    AssignmentEmailDispatcher dispatcher,
    IConfiguration configuration,
    IAppraisalUnitOfWork unitOfWork) : ICommandHandler<ResendAssignmentEmailCommand, ResendAssignmentEmailResult>
{
    public async Task<ResendAssignmentEmailResult> Handle(ResendAssignmentEmailCommand command, CancellationToken ct)
    {
        AssignmentEmailAccess.EnsureAllowed(currentUser);
        var sentBy = currentUser.UserCode?.Trim();
        if (string.IsNullOrWhiteSpace(sentBy))
            throw new ForbiddenException("The current user could not be determined, so the email cannot be sent. Sign in again.");

        var @case = await caseReader.GetCaseAsync(command.AppraisalId, ct)
                    ?? throw new AppraisalNotFoundException(command.AppraisalId);

        if (@case.IsClosed)
            throw new BadRequestException("The case is closed, so the email to the company can no longer be sent.");

        if (!@case.IsAssignedToCompany)
            throw new BadRequestException("The case is not assigned to an external company, so there is no one to email.");
        var assignmentId = @case.AssignmentId!.Value;

        if ((await emails.GetLatestUnsentAsync(assignmentId, ct))?.Status == AssignmentEmailStatus.Pending)
            throw new BadRequestException("The email chosen at assignment has not been sent yet, so it cannot be sent again.");

        var input = command.Email;
        var attachments = await AssignmentEmailAttachments.CheckAsync(input.Attachments, caseReader, configuration, ct);

        var email = AssignmentEmail.Resend(
            command.AppraisalId, assignmentId, input.ExtraTo ?? [], input.Cc ?? [], input.Subject, input.BodyHtml, attachments);
        // ผู้รับผิดชอบ as the preview names it: EXTO's keyer, EXT's follow-up officer.
        var (responsible, responsibleUserId) = @case.IsOffline
            ? (@case.KeyerName, @case.KeyerUserId)
            : (@case.FollowupName, @case.FollowupUserId);

        // The admin is waiting for an answer: an email that would reach nobody, or name nobody, is refused, not sent.
        await dispatcher.QueueAsync(email, @case.AssigneeCompanyId, responsible, responsibleUserId, sentBy,
            refuseIfIncomplete: true, ct);
        await emails.AddAsync(email, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ResendAssignmentEmailResult(email.Id);
    }
}
