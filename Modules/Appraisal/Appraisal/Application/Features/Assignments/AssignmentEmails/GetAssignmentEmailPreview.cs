using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Auth.Contracts.Companies;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

public record GetAssignmentEmailPreviewQuery(Guid AppraisalId) : IQuery<AssignmentEmailPreviewResult>;

/// <summary>
/// The email the admin edits when pressing มอบหมาย (EXT / EXTO): the bank template filled from the case's current
/// assignment row. Nothing is saved; the edited email travels with the assign request.
/// </summary>
public class GetAssignmentEmailPreviewQueryHandler(
    IAssignmentEmailCaseReader caseReader,
    ICompanyLookupService companyLookup,
    ISender sender,
    ICurrentUserService currentUser,
    IConfiguration configuration) : IQueryHandler<GetAssignmentEmailPreviewQuery, AssignmentEmailPreviewResult>
{
    public async Task<AssignmentEmailPreviewResult> Handle(GetAssignmentEmailPreviewQuery query, CancellationToken cancellationToken)
    {
        AssignmentEmailAccess.EnsureAllowed(currentUser);

        var @case = await caseReader.GetCaseAsync(query.AppraisalId, cancellationToken)
                    ?? throw new AppraisalNotFoundException(query.AppraisalId);

        var company = Guid.TryParse(@case.AssigneeCompanyId, out var companyId)
            ? await companyLookup.GetByIdAsync(companyId, cancellationToken)
            : null;

        // EXTO (method Offline): the keyer is the bank's responsible person. EXT: the follow-up officer, known only
        // when the admin picked one by hand; อัตโนมัติ leaves the placeholder for the send to fill.
        var responsible = @case.IsOffline
            ? @case.KeyerName
            : string.Equals(@case.FollowupMethod, AssignmentDefaultValues.MethodManual, StringComparison.OrdinalIgnoreCase) ? @case.FollowupName : null;

        var fee = await sender.Send(new GetFeePreviewQuery(query.AppraisalId, null, null, null), cancellationToken);

        var values = new AssignmentEmailTemplate.Values(
            company is null ? null : string.IsNullOrWhiteSpace(company.NameLocal) ? company.Name : company.NameLocal,
            @case.CustomerName,
            @case.AppraisalNumber,
            @case.ContactName,
            @case.ContactPhone,
            // No billable base yet (no fee, or only a 01 awaiting approval): assign would refuse it, so no figure is invented.
            fee.HasBillableBase ? fee.Total : null,
            @case.FeePaymentText,
            responsible,
            @case.Remark);

        List<string> cc = @case.IsOffline
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
}
