using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Auth.Contracts.Companies;
using Auth.Contracts.Users;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

/// <summary>
/// The bank template filled from the case's current assignment row: the admin's preview, and the email sent when an
/// assigned case has none chosen (#620).
/// </summary>
public class AssignmentEmailComposer(
    IAssignmentEmailCaseReader caseReader,
    ICompanyLookupService companyLookup,
    IUserLookupService userLookup,
    ISender sender,
    IConfiguration configuration)
{
    /// <summary>
    /// The template from the stored row, as the admin saved it: the preview. Unlike the handover templates, it expands
    /// the Cc roles whose people the row already settles into their addresses; the rest stay role tokens, which the
    /// send expands.
    /// <para>
    /// The preview trusts the row: whoever it names as keyer (EXTO) or follow-up officer (EXT), by the admin's hand or
    /// because the system already resolved them, is shown (Cc chips and <c>{{internal_staff}}</c>); an unset field keeps
    /// the token / placeholder. Callers must save the assignment draft first: <c>SaveDraft</c> is a full replace of
    /// the assignee, follow-up and company fields, so after a route-back the row reflects THIS decision (an automatic
    /// pick leaves them empty) and no previous officer is shown. For a resend the row is the live assignment.
    /// </para>
    /// </summary>
    public Task<AssignmentEmailPreviewResult> ComposeAsync(Guid appraisalId, CancellationToken cancellationToken) =>
        ComposeAsync(appraisalId, handover: null, from: null, cancellationToken);

    /// <summary>
    /// A stored email (ส่งอีกครั้ง's pre-fill) shown with the same rule as the preview: its Cc roles are expanded when the
    /// current row knows the people, other roles stay tokens, addresses typed by hand stay as they are. The subject,
    /// body, extra To and attachments are the stored ones; company name and To come from the current row.
    /// </summary>
    public Task<AssignmentEmailPreviewResult> ComposeFromAsync(
        Guid appraisalId, StoredAssignmentEmail from, CancellationToken cancellationToken) =>
        ComposeAsync(appraisalId, handover: null, from, cancellationToken);

    /// <summary>
    /// The template at an EXTO handover, while the stored row may still show the decision before it (a route-back keeps
    /// the old selections): the case is off-system, <paramref name="keyerName"/> is the responsible person and the fee
    /// is priced as AssignAppraisal priced it.
    /// </summary>
    public Task<AssignmentEmailPreviewResult> ComposeForExtoHandoverAsync(
        Guid appraisalId, string? keyerName, CancellationToken cancellationToken) =>
        ComposeAsync(appraisalId, new Handover(IsOffline: true, Responsible: keyerName, CompanyId: null, FeeType: "external-offline", FeeMethod: "manual"), from: null, cancellationToken);

    /// <summary>
    /// The template when the workflow assigned an EXT case to <paramref name="companyId"/> with nobody pressing มอบหมาย
    /// (#622), read before that assignment is saved: the company and the fee question come from the assignment, and the
    /// responsible person stays <c>{{internal_staff}}</c> until the follow-up officer is known.
    /// </summary>
    public Task<AssignmentEmailPreviewResult> ComposeForExtHandoverAsync(
        Guid appraisalId, Guid companyId, bool isQuotation, CancellationToken cancellationToken) =>
        ComposeAsync(appraisalId, new Handover(IsOffline: false, Responsible: null, CompanyId: companyId, FeeType: "external",
                FeeMethod: isQuotation ? "quotation" : "manual"),
            from: null, cancellationToken);

    /// <summary>The facts of an assignment being saved, which the stored row does not show yet.</summary>
    private sealed record Handover(bool IsOffline, string? Responsible, Guid? CompanyId, string FeeType, string FeeMethod);

    private async Task<AssignmentEmailPreviewResult> ComposeAsync(
        Guid appraisalId, Handover? handover, StoredAssignmentEmail? from, CancellationToken cancellationToken)
    {
        var @case = await caseReader.GetCaseAsync(appraisalId, cancellationToken)
                    ?? throw new AppraisalNotFoundException(appraisalId);

        var companyId = handover?.CompanyId
                        ?? (Guid.TryParse(@case.AssigneeCompanyId, out var assigned) ? assigned : (Guid?)null);
        var company = companyId is { } id ? await companyLookup.GetByIdAsync(id, cancellationToken) : null;

        // EXTO (method Offline): the keyer is the bank's responsible person. EXT: the follow-up officer. Each is known
        // when the row names one; otherwise the placeholder is left for the send to fill.
        var isOffline = handover?.IsOffline ?? @case.IsOffline;

        if (from is not null)
        {
            var (storedCc, storedPeople) = await ExpandKnownRolesAsync(
                from.Cc, isOffline ? @case.KeyerUserId : @case.FollowupUserId, cancellationToken);
            return new AssignmentEmailPreviewResult(
                company is null ? null : DisplayName(company), CompanyTo(company), from.ExtraTo, storedCc, storedPeople,
                from.Subject, from.BodyHtml, from.Attachments,
                AssignmentEmailLimits.MaxAttachments, AssignmentEmailLimits.MaxAttachmentBytes(configuration));
        }
        var responsible = handover is not null ? handover.Responsible
            : isOffline ? @case.KeyerName
            : @case.FollowupName;

        // A handover asks what its assignment bills; the preview lets the row decide.
        var fee = await sender.Send(
            handover is not null
                ? new GetFeePreviewQuery(appraisalId, handover.FeeType, handover.FeeMethod, company?.Id)
                : new GetFeePreviewQuery(appraisalId, null, null, null),
            cancellationToken);

        var values = new AssignmentEmailTemplate.Values(
            company is null ? null : DisplayName(company),
            @case.CustomerName,
            @case.AppraisalNumber,
            @case.ContactName,
            @case.ContactPhone,
            // No billable base yet (no fee, or only a 01 awaiting approval): assign would refuse it, so no figure is invented.
            fee.HasBillableBase ? fee.Total : null,
            @case.FeePaymentText,
            responsible,
            @case.Remark);

        List<string> cc = isOffline
            ? [AssignmentEmailCcRoles.IntAdminAll, AssignmentEmailCcRoles.Keyer, AssignmentEmailCcRoles.KeyerSupervisor]
            : [AssignmentEmailCcRoles.IntAdminAll, AssignmentEmailCcRoles.FollowupStaff, AssignmentEmailCcRoles.FollowupSupervisor];
        List<AssignmentEmailPerson> ccPeople = [];
        if (handover is null)
        {
            // Who the keyer / follow-up chips name: whoever the row holds (see ComposeAsync); none → the tokens stay.
            var responsibleUserId = isOffline ? @case.KeyerUserId : @case.FollowupUserId;
            (cc, ccPeople) = await ExpandKnownRolesAsync(cc, responsibleUserId, cancellationToken);
        }

        return new AssignmentEmailPreviewResult(
            company is null ? null : DisplayName(company),
            CompanyTo(company),
            [],
            cc,
            ccPeople,
            AssignmentEmailTemplate.Subject(values),
            AssignmentEmailTemplate.BodyHtml(values),
            [],
            AssignmentEmailLimits.MaxAttachments,
            AssignmentEmailLimits.MaxAttachmentBytes(configuration));
    }

    /// <summary>
    /// Each role whose people are known becomes their addresses (duplicates dropped); the others, and a known role with
    /// nobody usable, stay tokens. Same lookup as the send, so the preview lists the people the send will mail.
    /// </summary>
    private async Task<(List<string> Cc, List<AssignmentEmailPerson> People)> ExpandKnownRolesAsync(
        List<string> roles, string? responsibleUserId, CancellationToken ct)
    {
        var cc = new List<string>();
        var addresses = new List<(string Email, string Stored)>(); // each address with the stored email it came from
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var storedEmails = new List<string>();
        foreach (var role in roles)
        {
            if (!AssignmentEmailCcRoles.All.Contains(role))
            {
                // An address typed by hand (a stored email's Cc): as it is, once.
                if (seen.Add(role))
                {
                    storedEmails.Add(role);
                    addresses.Add((role, role));
                    cc.Add(role);
                }

                continue;
            }

            if (AssignmentEmailCcRoleExpansion.NeedsResponsible(role) && string.IsNullOrWhiteSpace(responsibleUserId))
            {
                cc.Add(role);
                continue;
            }

            var found = false;
            foreach (var stored in await AssignmentEmailCcRoleExpansion.EmailsAsync(userLookup, role, responsibleUserId, ct))
            {
                // A stored email may hold several ("a; b"), or junk: only the plain parts become chips.
                storedEmails.Add(stored);
                foreach (var email in AssignmentEmailCcRoleExpansion.PlainAddresses([stored]))
                {
                    found = true;
                    if (seen.Add(email))
                    {
                        addresses.Add((email, stored));
                        cc.Add(email);
                    }
                }
            }

            // Known but nobody usable (yet): the chip stays, and the send resolves it again.
            if (!found)
                cc.Add(role);
        }

        IReadOnlyDictionary<string, string> names = addresses.Count == 0
            ? new Dictionary<string, string>()
            : await userLookup.GetNamesByEmailAsync(storedEmails, ct);
        return (cc, addresses.Select(a => new AssignmentEmailPerson(a.Email, names.GetValueOrDefault(a.Stored))).ToList());
    }

    /// <summary>The company's usable address(es) as the send would use them; null when there are none.</summary>
    private static string? CompanyTo(CompanyLookupDto? company) =>
        AssignmentEmailCcRoleExpansion.PlainAddresses([company?.Email]) is { Count: > 0 } to ? string.Join("; ", to) : null;

    /// <summary>The company as the Thai email names it: its Thai name when it has one.</summary>
    public static string DisplayName(CompanyLookupDto company) =>
        string.IsNullOrWhiteSpace(company.NameLocal) ? company.Name : company.NameLocal;
}
