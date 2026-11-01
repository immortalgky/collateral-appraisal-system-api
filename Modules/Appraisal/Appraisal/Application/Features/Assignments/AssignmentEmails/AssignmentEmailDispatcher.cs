using Auth.Contracts.Companies;
using Auth.Contracts.Users;
using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

/// <summary>
/// Readies the email to the external company when an EXTO case is handed over to its keyer (#620) and queues it on the
/// outbox, to be committed by the caller's save together with the assignment. Never saves itself.
/// EXT's handover (#622) will need its own responsible person, Cc roles and fee question.
/// </summary>
public class AssignmentEmailDispatcher(
    IAssignmentEmailRepository emails,
    AssignmentEmailComposer composer,
    ICompanyLookupService companyLookup,
    IUserLookupService userLookup,
    IIntegrationEventOutbox outbox)
{
    /// <param name="keyerUserId">The keyer the case was handed to, a username.</param>
    public async Task QueueExtoAsync(AppraisalAssignment assignment, string? keyerUserId, CancellationToken ct)
    {
        var appraisalId = assignment.AppraisalId;
        var email = await emails.GetLatestUnsentAsync(assignment.Id, ct);
        if (email?.Status == AssignmentEmailStatus.NotSent)
            return;

        var responsible = await DisplayNameAsync(keyerUserId, ct);
        var template = email is null ? await composer.ComposeForExtoHandoverAsync(appraisalId, responsible, ct) : null;

        // No choice recorded (an assign that did not carry one): the bank template, as the preview would show it.
        email ??= AssignmentEmail.ToSend(
            appraisalId, assignment.Id, template!.ExtraTo, template.Cc, template.Subject, template.BodyHtml, []);

        await QueueAsync(email, assignment.AssigneeCompanyId, responsible, assignment.AssignedBy, refuseIfIncomplete: false, ct);
        // Added only once queued: a failed read must not leave a Pending template row for the caller's save.
        if (template is not null)
            await emails.AddAsync(email, ct);
    }

    /// <summary>
    /// Fills the company / responsible placeholders, resolves the recipients and hands <paramref name="email"/> to the
    /// sender (outbox): the EXTO handover and a resend from the slip (#623). The company is read before the email
    /// changes, so a failed read leaves nothing half-queued.
    /// </summary>
    /// <param name="refuseIfIncomplete">A resend, with the admin waiting for the answer: an email that would reach nobody,
    /// or whose <c>{{company}}</c> / <c>{{responsible}}</c> is still unknown, is a 400 before anything is queued.
    /// The handover still hands it over, so the failure is reported there (#621/#624).</param>
    public async Task QueueAsync(
        AssignmentEmail email,
        string? assigneeCompanyId,
        string? responsible,
        string? assignedBy,
        bool refuseIfIncomplete,
        CancellationToken ct)
    {
        var company = Guid.TryParse(assigneeCompanyId, out var companyId)
            ? await companyLookup.GetByIdAsync(companyId, ct)
            : null;

        var to = new[] { company?.Email }.Concat(email.ExtraTo)
            .Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // ponytail: role tokens are dropped until #621 expands them to addresses.
        var cc = email.Cc.Where(c => !AssignmentEmailCcRoles.All.Contains(c)).ToList();
        var companyName = company is null ? null : AssignmentEmailComposer.DisplayName(company);
        if (refuseIfIncomplete)
        {
            if (to.Count == 0)
                throw new BadRequestException("The company has no email address. Add a To address to send it.");
            var unknown = Unknown(AssignmentEmail.CompanyPlaceholder, companyName) ? AssignmentEmail.CompanyPlaceholder
                : Unknown(AssignmentEmail.ResponsiblePlaceholder, responsible) ? AssignmentEmail.ResponsiblePlaceholder
                : null;
            if (unknown is not null)
                throw new BadRequestException($"The email still has {unknown}, and its value is not known yet. Replace it in the email.");
        }

        email.Queue(companyName, responsible, to, cc);

        bool Unknown(string placeholder, string? value) =>
            string.IsNullOrWhiteSpace(value) && email.BodyHtml.Contains(placeholder, StringComparison.Ordinal);

        outbox.Publish(new AssignmentEmailRequestedIntegrationEvent
        {
            AppraisalId = email.AppraisalId,
            EmailId = email.Id,
            To = to,
            Cc = cc,
            Subject = email.Subject,
            HtmlBody = email.BodyHtml,
            AttachmentRefs = email.Attachments
                .Select(a => new EmailAttachmentRefData("document", a.DocumentId.ToString())).ToList(),
            AssignedBy = assignedBy
        }, correlationId: email.AppraisalId.ToString());
    }

    private async Task<string?> DisplayNameAsync(string? username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        var users = await userLookup.GetByUsernamesAsync([username], ct);
        return users.TryGetValue(username, out var user) && $"{user.FirstName} {user.LastName}".Trim() is { Length: > 0 } name
            ? name
            : username;
    }
}
