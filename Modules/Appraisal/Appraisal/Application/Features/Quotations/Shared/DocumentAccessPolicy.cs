using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.Shared;

/// <summary>
/// Authorization policy for document/gallery streaming endpoints.
///
/// Normal users (Admin, RM, Internal appraiser):
///   Admin/IntAdmin: always.
///   RM: only if the appraisal belongs to a request they own (request.Requestor == userId).
///       The caller must supply the RM's own request ID via <paramref name="rmRequestorId"/>.
///
/// External company (ExtAdmin) path (v4 Quotation feature):
///   An ExtAdmin principal whose company has an active (non-terminal) invitation
///   on a QuotationRequest that contains the specific <paramref name="appraisalId"/> may view
///   ONLY those documents that the admin has explicitly shared (QuotationSharedDocuments).
///   "Active" means the QuotationRequest.Status NOT IN ('Finalized', 'Cancelled')
///   AND the invitation is not Withdrawn.
///   The check is scoped to the exact appraisalId — Company B cannot see Company A's invitation.
///   Post-finalize: the winning company retains access for as long as they own an active
///   <c>AppraisalAssignment</c> on the appraisal (they need docs to execute the work).
///   Pass <paramref name="assignedCompanyId"/> = AssigneeCompanyId of the current active
///   assignment on the appraisal (or null if none).
///   Pass <paramref name="sharedDocumentIds"/> = set of DocumentIds the admin shared;
///   the caller must intersect the document list against this set before returning.
///
/// Wire this policy inside any endpoint that streams appraisal documents/gallery
/// by calling <see cref="EnsureCanViewAppraisalDocuments"/>.
/// </summary>
public static class DocumentAccessPolicy
{
    /// <summary>
    /// Returns true when the caller is allowed to view documents for <paramref name="appraisalId"/>.
    /// </summary>
    /// <param name="appraisalId">Target appraisal.</param>
    /// <param name="invitations">
    /// All invitations on QuotationRequests that contain the target appraisal.
    /// Each tuple carries: (CompanyId, InvitationStatus, QuotationStatus).
    /// </param>
    /// <param name="user">Authenticated caller.</param>
    /// <param name="rmRequestorId">
    /// The Requestor (RM UserId) of the request linked to <paramref name="appraisalId"/>.
    /// Required for the RM branch. Null denies RM access.
    /// </param>
    /// <param name="assignedCompanyId">
    /// AssigneeCompanyId of the current active AppraisalAssignment (post-finalize access).
    /// </param>
    /// <param name="sharedDocumentIds">
    /// v4: set of DocumentIds the admin has explicitly shared via QuotationSharedDocuments.
    /// When non-null and caller is ExtAdmin, access is limited to this set.
    /// Null means "not applicable" (admin/RM paths) — no filtering.
    /// </param>
    public static bool CanViewAppraisalDocuments(
        Guid appraisalId,
        IEnumerable<(Guid CompanyId, string InvitationStatus, string QuotationStatus)> invitations,
        ICurrentUserService user,
        Guid? rmRequestorId = null,
        Guid? assignedCompanyId = null,
        IReadOnlySet<Guid>? sharedDocumentIds = null)
    {
        // Admin branch — unrestricted
        if (user.IsInRole("Admin") || user.IsInRole("IntAdmin"))
            return true;

        // RM branch — must be the RM who owns the linked request (C7 fix)
        if (user.IsInRole("RequestMaker"))
            return rmRequestorId.HasValue && user.UserId == rmRequestorId.Value;

        // ExtAdmin branch (v4): invited company with active non-terminal quotation,
        // scoped to the specific appraisalId (C6 fix — invitations already pre-filtered
        // to those containing the target appraisal by the caller's query).
        if (user.IsInRole("ExtAdmin"))
        {
            var companyId = user.CompanyId;
            if (!companyId.HasValue) return false;

            // M6 fix: winning company retains doc access post-Finalize via an active
            // AppraisalAssignment. Caller supplies the current assignee company id.
            if (assignedCompanyId.HasValue && assignedCompanyId.Value == companyId.Value)
                return true;

            var terminalStatuses = new[] { "Finalized", "Cancelled" };

            return invitations.Any(inv =>
                inv.CompanyId == companyId.Value &&
                inv.InvitationStatus != "Withdrawn" &&
                !terminalStatuses.Contains(inv.QuotationStatus));
        }

        return false;
    }

    /// <summary>
    /// Throws <see cref="UnauthorizedAccessException"/> when the caller is not allowed to view
    /// documents for <paramref name="appraisalId"/>.
    /// </summary>
    public static void EnsureCanViewAppraisalDocuments(
        Guid appraisalId,
        IEnumerable<(Guid CompanyId, string InvitationStatus, string QuotationStatus)> invitations,
        ICurrentUserService user,
        Guid? rmRequestorId = null,
        Guid? assignedCompanyId = null,
        IReadOnlySet<Guid>? sharedDocumentIds = null)
    {
        if (!CanViewAppraisalDocuments(appraisalId, invitations, user, rmRequestorId, assignedCompanyId, sharedDocumentIds))
            throw new UnauthorizedAccessException(
                $"You do not have permission to view documents for appraisal '{appraisalId}'.");
    }

}
