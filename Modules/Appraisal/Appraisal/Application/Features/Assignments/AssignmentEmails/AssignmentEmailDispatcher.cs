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

        // Every read happens before anything is added or published, so a failed read leaves nothing half-queued.
        var company = Guid.TryParse(assignment.AssigneeCompanyId, out var companyId)
            ? await companyLookup.GetByIdAsync(companyId, ct)
            : null;
        var responsible = await DisplayNameAsync(keyerUserId, ct);
        var template = email is null ? await composer.ComposeForExtoHandoverAsync(appraisalId, responsible, ct) : null;

        // No choice recorded (an assign that did not carry one): the bank template, as the preview would show it.
        if (template is not null)
        {
            email = AssignmentEmail.ToSend(
                appraisalId, assignment.Id, template.ExtraTo, template.Cc, template.Subject, template.BodyHtml, []);
            await emails.AddAsync(email, ct);
        }

        email.Queue(company is null ? null : AssignmentEmailComposer.DisplayName(company), responsible);

        outbox.Publish(new AssignmentEmailRequestedIntegrationEvent
        {
            AppraisalId = appraisalId,
            EmailId = email.Id,
            To = new[] { company?.Email }.Concat(email.ExtraTo)
                .Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            // ponytail: role tokens are dropped until #621 expands them to addresses.
            Cc = email.Cc.Where(c => !AssignmentEmailCcRoles.All.Contains(c)).ToList(),
            Subject = email.Subject,
            HtmlBody = email.BodyHtml,
            AttachmentRefs = email.Attachments
                .Select(a => new EmailAttachmentRefData("document", a.DocumentId.ToString())).ToList(),
            AssignedBy = assignment.AssignedBy
        }, correlationId: appraisalId.ToString());
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
