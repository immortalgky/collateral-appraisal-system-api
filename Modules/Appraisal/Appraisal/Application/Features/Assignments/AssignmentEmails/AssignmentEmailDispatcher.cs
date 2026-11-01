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
    IIntegrationEventOutbox outbox,
    ILogger<AssignmentEmailDispatcher> logger)
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

        await QueueAsync(email, assignment.AssigneeCompanyId, responsible, keyerUserId, assignment.AssignedBy,
            refuseIfIncomplete: false, ct);
        // Added only once queued (or failed): a failed read must not leave a Pending template row for the caller's save.
        if (template is not null)
            await emails.AddAsync(email, ct);
    }

    /// <summary>
    /// Fills the company / responsible placeholders, resolves the recipients and hands <paramref name="email"/> to the
    /// sender (outbox): the EXTO handover and a resend from the slip (#623). Every read happens before the email
    /// changes, so a failed read leaves nothing half-queued. An email with no To is marked Failed, not published.
    /// </summary>
    /// <param name="responsibleUserId">The keyer / follow-up officer as a username, for the Cc chips that name them.</param>
    /// <param name="refuseIfIncomplete">A resend, with the admin waiting for the answer: an email that would reach nobody,
    /// or whose <c>{{company}}</c> / <c>{{responsible}}</c> is still unknown, is a 400 before anything is queued.
    /// The handover still hands it over, and an email with no To fails there (#621/#624).</param>
    public async Task QueueAsync(
        AssignmentEmail email,
        string? assigneeCompanyId,
        string? responsible,
        string? responsibleUserId,
        string? assignedBy,
        bool refuseIfIncomplete,
        CancellationToken ct)
    {
        var company = Guid.TryParse(assigneeCompanyId, out var companyId)
            ? await companyLookup.GetByIdAsync(companyId, ct)
            : null;
        var cc = await ExpandCcAsync(email.Cc, responsibleUserId, ct);

        // The company's own address or the admin's extra To: with neither there is nobody to send to, whoever is in Cc.
        var to = Clean(new[] { company?.Email }.Concat(email.ExtraTo));
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

        if (to.Count == 0)
        {
            var reason = company is null
                ? "No company is assigned, or it could not be found, and no other To address was added."
                : "The company has no email address and no other To address was added.";
            email.Fail(reason);
            logger.LogWarning(
                "Assignment email {EmailId} for appraisal {AppraisalId} (company {CompanyId}) marked Failed: {Reason}",
                email.Id, email.AppraisalId, company?.Id, reason);
            return;
        }

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

        bool Unknown(string placeholder, string? value) =>
            string.IsNullOrWhiteSpace(value) && email.BodyHtml.Contains(placeholder, StringComparison.Ordinal);
    }

    /// <summary>
    /// The stored Cc as addresses: literal addresses as they are, each remaining role chip through Auth (which skips
    /// inactive, expired and email-less users). A chip the admin removed is not in the list, so it is not expanded.
    /// Keyer and follow-up chips name <paramref name="responsibleUserId"/>, the person the case was handed to.
    /// </summary>
    private async Task<List<string>> ExpandCcAsync(IEnumerable<string> cc, string? responsibleUserId, CancellationToken ct)
    {
        var addresses = new List<string>();
        foreach (var entry in cc)
        {
            addresses.AddRange(entry switch
            {
                AssignmentEmailCcRoles.IntAdminAll => await userLookup.GetEmailsInRoleAsync(IntAdminRole, ct),
                AssignmentEmailCcRoles.Keyer or AssignmentEmailCcRoles.FollowupStaff =>
                    string.IsNullOrWhiteSpace(responsibleUserId) ? [] : await userLookup.GetEmailsAsync([responsibleUserId], ct),
                AssignmentEmailCcRoles.KeyerSupervisor or AssignmentEmailCcRoles.FollowupSupervisor =>
                    string.IsNullOrWhiteSpace(responsibleUserId) ? [] : await userLookup.GetSupervisorEmailsAsync(responsibleUserId, ct),
                _ => [entry]
            });
        }

        return Clean(addresses);
    }

    /// <summary>
    /// One address per entry, de-duplicated. A master-data address may hold several ("a; b"): split on ',' / ';' as
    /// the Notification module's EmailRecipients does, so an entry of separators only counts as no address.
    /// </summary>
    private static List<string> Clean(IEnumerable<string?> addresses) =>
        addresses.SelectMany(a => (a ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private const string IntAdminRole = "IntAdmin";

    private async Task<string?> DisplayNameAsync(string? username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        var users = await userLookup.GetByUsernamesAsync([username], ct);
        return users.TryGetValue(username, out var user) && $"{user.FirstName} {user.LastName}".Trim() is { Length: > 0 } name
            ? name
            : username;
    }
}
