using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Auth.Contracts.Companies;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

/// <summary>
/// The bank template filled from the case's current assignment row: the admin's preview, and the email sent when an
/// assigned case has none chosen (#620).
/// </summary>
public class AssignmentEmailComposer(
    IAssignmentEmailCaseReader caseReader,
    ICompanyLookupService companyLookup,
    ISender sender,
    IConfiguration configuration)
{
    /// <summary>The template from the stored row, as the admin saved it: the preview.</summary>
    public Task<AssignmentEmailPreviewResult> ComposeAsync(Guid appraisalId, CancellationToken cancellationToken) =>
        ComposeAsync(appraisalId, extoHandover: false, keyerName: null, cancellationToken);

    /// <summary>
    /// The template at an EXTO handover, while the stored row may still show the decision before it (a route-back keeps
    /// the old selections): the case is off-system, <paramref name="keyerName"/> is the responsible person and the fee
    /// is priced as AssignAppraisal priced it.
    /// </summary>
    public Task<AssignmentEmailPreviewResult> ComposeForExtoHandoverAsync(
        Guid appraisalId, string? keyerName, CancellationToken cancellationToken) =>
        ComposeAsync(appraisalId, extoHandover: true, keyerName, cancellationToken);

    private async Task<AssignmentEmailPreviewResult> ComposeAsync(
        Guid appraisalId, bool extoHandover, string? keyerName, CancellationToken cancellationToken)
    {
        var @case = await caseReader.GetCaseAsync(appraisalId, cancellationToken)
                    ?? throw new AppraisalNotFoundException(appraisalId);

        var company = Guid.TryParse(@case.AssigneeCompanyId, out var companyId)
            ? await companyLookup.GetByIdAsync(companyId, cancellationToken)
            : null;

        // EXTO (method Offline): the keyer is the bank's responsible person. EXT: the follow-up officer, known only
        // when the admin picked one by hand; อัตโนมัติ leaves the placeholder for the send to fill.
        var isOffline = extoHandover || @case.IsOffline;
        var responsible = extoHandover ? keyerName
            : isOffline ? @case.KeyerName
            : string.Equals(@case.FollowupMethod, AssignmentDefaultValues.MethodManual, StringComparison.OrdinalIgnoreCase) ? @case.FollowupName : null;

        // The handover asks what AssignAppraisal asked for EXTO; the preview lets the row decide.
        var fee = await sender.Send(
            extoHandover
                ? new GetFeePreviewQuery(appraisalId, "external-offline", "manual", company?.Id)
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

        return new AssignmentEmailPreviewResult(
            company?.Email,
            [],
            cc,
            AssignmentEmailTemplate.Subject(values),
            AssignmentEmailTemplate.BodyHtml(values),
            [],
            AssignmentEmailLimits.MaxAttachments,
            AssignmentEmailLimits.MaxAttachmentBytes(configuration));
    }

    /// <summary>The company as the Thai email names it: its Thai name when it has one.</summary>
    public static string DisplayName(CompanyLookupDto company) =>
        string.IsNullOrWhiteSpace(company.NameLocal) ? company.Name : company.NameLocal;
}
