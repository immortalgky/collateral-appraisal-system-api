using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;
using Appraisal.Application.Features.Fees.GetFeePreview;
using Auth.Contracts.Companies;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

/// <param name="Template">True for "คืนค่า template": the filled template even when a draft is saved.</param>
public record GetAssignmentEmailDraftQuery(Guid AppraisalId, bool Template = false) : IQuery<AssignmentEmailDraftResult>;

/// <summary>The appraisal's saved email draft, or the bank template filled from the case when there is none.</summary>
public class GetAssignmentEmailDraftQueryHandler(
    AppraisalDbContext dbContext,
    IAssignmentEmailCaseReader caseReader,
    ICompanyLookupService companyLookup,
    ISender sender,
    ICurrentUserService currentUser,
    IConfiguration configuration) : IQueryHandler<GetAssignmentEmailDraftQuery, AssignmentEmailDraftResult>
{
    public async Task<AssignmentEmailDraftResult> Handle(GetAssignmentEmailDraftQuery query, CancellationToken cancellationToken)
    {
        AssignmentEmailAccess.EnsureAllowed(currentUser);

        var @case = await caseReader.GetCaseAsync(query.AppraisalId, cancellationToken)
                    ?? throw new AppraisalNotFoundException(query.AppraisalId);

        var company = Guid.TryParse(@case.AssigneeCompanyId, out var companyId)
            ? await companyLookup.GetByIdAsync(companyId, cancellationToken)
            : null;
        var maxAttachmentBytes = AssignmentEmailLimits.MaxAttachmentBytes(configuration);

        var saved = query.Template ? null : await dbContext.AssignmentEmails
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.AppraisalId == query.AppraisalId && e.Status == AssignmentEmailStatus.Draft,
                cancellationToken);
        // A stale draft (reassigned, or another company) is not shown; the next save takes the row over.
        if (saved is not null && !saved.IsFor(@case.AssignmentId, @case.AssigneeCompanyId))
            saved = null;
        if (saved is not null)
        {
            var documents = (await caseReader.GetDocumentsAsync(
                    saved.Attachments.Select(a => a.DocumentId).ToList(), cancellationToken))
                .ToDictionary(d => d.Id);
            return new AssignmentEmailDraftResult(
                saved.Id,
                company?.Email,
                saved.ExtraTo,
                saved.Cc,
                saved.Subject,
                saved.BodyHtml,
                // A document deleted since the save is still listed, without a name or size, so the admin sees it.
                saved.Attachments
                    .Select(a => documents.TryGetValue(a.DocumentId, out var d)
                        ? new AssignmentEmailAttachmentDto(a.DocumentId, a.Source, d.FileName, d.FileSizeBytes)
                        : new AssignmentEmailAttachmentDto(a.DocumentId, a.Source, null, 0))
                    .ToList(),
                AssignmentEmailLimits.MaxAttachments,
                maxAttachmentBytes,
                saved.UpdatedAt ?? saved.CreatedAt);
        }

        // EXTO (method Offline): the keyer is the bank's responsible person. EXT: the follow-up officer, known only
        // when the admin picked one by hand; อัตโนมัติ leaves the placeholder for the send to fill.
        var isOffline = string.Equals(
            @case.AssignmentMethod, AppraisalAssignment.OfflineAssignmentMethod, StringComparison.OrdinalIgnoreCase);
        var responsible = isOffline
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

        List<string> cc = isOffline
            ? [AssignmentEmailCcRoles.IntAdminAll, AssignmentEmailCcRoles.Keyer, AssignmentEmailCcRoles.KeyerSupervisor]
            : [AssignmentEmailCcRoles.IntAdminAll, AssignmentEmailCcRoles.FollowupStaff, AssignmentEmailCcRoles.FollowupSupervisor];

        return new AssignmentEmailDraftResult(
            null,
            company?.Email,
            [],
            cc,
            AssignmentEmailTemplate.Subject(values),
            AssignmentEmailTemplate.BodyHtml(values),
            [],
            AssignmentEmailLimits.MaxAttachments,
            maxAttachmentBytes,
            null);
    }
}
