using Auth.Contracts.Companies;
using Auth.Contracts.Users;
using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

/// <summary>
/// Readies the email to the external company when an EXTO case is handed over to its keyer (#620) and queues it on the
/// outbox, to be committed by the caller's save together with the assignment. Never saves itself.
/// EXT (#622): the template row is added when the company is assigned, and sent once the follow-up is known.
/// </summary>
public class AssignmentEmailDispatcher(
    IAssignmentEmailRepository emails,
    AssignmentEmailComposer composer,
    ICompanyLookupService companyLookup,
    IUserLookupService userLookup,
    IIntegrationEventOutbox outbox)
{
    /// <param name="appraisalNumber">App No., carried for the failure notice (#624).</param>
    /// <param name="keyerUserId">The keyer the case was handed to, a username.</param>
    public async Task QueueExtoAsync(
        AppraisalAssignment assignment, string? appraisalNumber, string? keyerUserId, CancellationToken ct)
    {
        var appraisalId = assignment.AppraisalId;
        var email = await emails.GetLatestUnsentAsync(assignment.Id, ct);
        if (email?.Status == AssignmentEmailStatus.NotSent)
            return;

        var responsible = await DisplayNameAsync(keyerUserId, ct);
        var template = email is null ? await composer.ComposeForExtoHandoverAsync(appraisalId, responsible, ct) : null;

        // No choice recorded (an assign that did not carry one): the bank template, as the preview would show it.
        email ??= FromTemplate(assignment, template!);

        await QueueAsync(email, appraisalNumber, assignment.AssigneeCompanyId, responsible, keyerUserId, assignment.AssignedBy,
            refuseIfIncomplete: false, ct);
        // Added only once queued (or failed): a failed read must not leave a Pending template row for the caller's save.
        if (template is not null)
            await emails.AddAsync(email, ct);
    }

    /// <summary>
    /// EXT (#622), when the workflow assigned <paramref name="companyId"/> as a new engagement (the row was Pending, or
    /// held another company; not a route-back replay) and nobody pressed มอบหมาย, so no email was chosen: adds the bank template
    /// as a Pending row, for the caller's save. <see cref="QueueExtAsync"/> sends it once the follow-up is known.
    /// </summary>
    public async Task AddExtTemplateAsync(
        AppraisalAssignment assignment, Guid companyId, bool isQuotation, CancellationToken ct)
    {
        if (await emails.GetLatestUnsentAsync(assignment.Id, ct) is not null)
            return; // the admin's choice, sent or not

        var template = await composer.ComposeForExtHandoverAsync(assignment.AppraisalId, companyId, isQuotation, ct);
        await emails.AddAsync(FromTemplate(assignment, template), ct);
    }

    private static AssignmentEmail FromTemplate(AppraisalAssignment assignment, AssignmentEmailPreviewResult template) =>
        AssignmentEmail.ToSend(
            assignment.AppraisalId, assignment.Id, template.ExtraTo, template.Cc, template.Subject, template.BodyHtml, []);

    /// <summary>
    /// EXT (#622): the follow-up officer is now known, so the email chosen for this engagement goes out. That is the
    /// admin's Pending row, or the template row CompanyAssigned added for a system assignment. No such row (NotSent, or
    /// a route-back replay of the company the case already holds) → nothing.
    /// </summary>
    /// <param name="followupUserId">The follow-up officer, a username; null when the selection found nobody.</param>
    public async Task QueueExtAsync(
        AppraisalAssignment assignment, string? appraisalNumber, string? followupUserId, CancellationToken ct)
    {
        var email = await emails.GetLatestUnsentAsync(assignment.Id, ct);
        if (email is not { Status: AssignmentEmailStatus.Pending })
            return;

        // Nobody to follow up (no_match): "-" rather than a raw placeholder in the company's email (user, 2026-11-01).
        var responsible = followupUserId is null ? "-" : await DisplayNameAsync(followupUserId, ct);
        await QueueAsync(email, appraisalNumber, assignment.AssigneeCompanyId, responsible, followupUserId,
            assignment.AssignedBy, refuseIfIncomplete: false, ct);
    }

    /// <summary>
    /// Fills the company / responsible placeholders, resolves the recipients and hands <paramref name="email"/> to the
    /// sender (outbox): the EXTO handover and a resend from the slip (#623). Every read happens before the email
    /// changes, so a failed read leaves nothing half-queued. An email with no To is still published: Notification marks
    /// it Failed and tells the admins (#624).
    /// </summary>
    /// <param name="responsibleUserId">The keyer / follow-up officer as a username, for the Cc chips that name them.</param>
    /// <param name="refuseIfIncomplete">A resend, with the admin waiting for the answer: an email that would reach nobody,
    /// or whose <c>{{company}}</c> / <c>{{responsible}}</c> is still unknown, is a 400 before anything is queued.
    /// The handover still hands it over, and Notification fails an email with no To (#624).</param>
    public async Task QueueAsync(
        AssignmentEmail email,
        string? appraisalNumber,
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
            AssignedBy = assignedBy,
            AppraisalNumber = appraisalNumber,
            CompanyName = companyName
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
