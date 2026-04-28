using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.Shared;

/// <summary>
/// Centralises all role-based access rules for the quotation feature.
/// Invoke inside command/query handlers — not only in Carter endpoints —
/// so that any caller path (background service, integration handler, etc.) is also protected.
/// </summary>
public static class QuotationAccessPolicy
{
    // ─────────────────────────────────────────────────────────────────────────
    // Role checks
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures the caller is an internal admin (role "Admin" or "IntAdmin").
    /// Throws UnauthorizedAccessException if not.
    /// </summary>
    public static void EnsureAdmin(ICurrentUserService user)
    {
        if (!user.IsInRole("Admin") && !user.IsInRole("IntAdmin"))
            throw new UnauthorizedAccessException("Only Admin users can perform this operation");
    }

    /// <summary>
    /// Ensures the caller is an RM who matches the quotation's RmUserId, or an Admin (who may override).
    /// Pass <paramref name="quotation"/> so the check uses the denormalized RmUserId field;
    /// this avoids a cross-module Request lookup inside the handler.
    /// </summary>
    public static void EnsureRmOrAdmin(QuotationRequest quotation, ICurrentUserService user)
    {
        if (user.IsInRole("Admin") || user.IsInRole("IntAdmin"))
            return;

        if (!user.IsInRole("RequestMaker"))
            throw new UnauthorizedAccessException("Only RM or Admin users can perform this operation");

        if (user.UserId != quotation.RmUserId)
            throw new UnauthorizedAccessException("RM can only perform this operation for their own requests");
    }

    /// <summary>
    /// Overload that accepts a raw <paramref name="requestorId"/> for callers that do not have
    /// the full aggregate (e.g., legacy code paths).
    /// </summary>
    public static void EnsureRmOrAdmin(Guid? requestorId, ICurrentUserService user)
    {
        if (user.IsInRole("Admin") || user.IsInRole("IntAdmin"))
            return;

        if (!user.IsInRole("RequestMaker"))
            throw new UnauthorizedAccessException("Only RM or Admin users can perform this operation");

        if (user.UserId != requestorId)
            throw new UnauthorizedAccessException("RM can only perform this operation for their own requests");
    }

    /// <summary>
    /// Ensures the caller can view the given quotation request.
    ///   Admin: always.
    ///   RM: requestor match AND status ∈ {PendingRmSelection, WinnerTentative, Negotiating, Finalized}.
    ///   ExtAdmin (ExtCompany): must be an invited company.
    /// </summary>
    public static void EnsureCanViewQuotation(
        QuotationRequest quotation,
        Guid? requestorId,
        ICurrentUserService user)
    {
        if (user.IsInRole("Admin") || user.IsInRole("IntAdmin"))
            return;

        if (user.IsInRole("RequestMaker"))
        {
            if (user.UserId != requestorId)
                throw new UnauthorizedAccessException("RM can only view quotations for their own requests");

            var rmVisibleStatuses = new[]
            {
                "PendingRmSelection", "WinnerTentative", "Negotiating", "Finalized"
            };
            if (!rmVisibleStatuses.Contains(quotation.Status))
                throw new UnauthorizedAccessException(
                    $"RM cannot view quotation in status '{quotation.Status}'");

            return;
        }

        if (user.IsInRole("ExtAdmin") || user.IsInRole("ExtAppraisalChecker"))
        {
            var companyId = user.CompanyId;
            if (!companyId.HasValue)
                throw new UnauthorizedAccessException("External company user has no company claim");

            var invitation = quotation.Invitations.FirstOrDefault(i => i.CompanyId == companyId.Value);
            if (invitation is null)
                throw new UnauthorizedAccessException("External company is not invited to this quotation");

            // M4: reject if the company's bid has been Withdrawn.
            // Exception: if quotation is Finalized and this company was the winner, allow view for record purposes.
            var companyBid = quotation.Quotations.FirstOrDefault(q => q.CompanyId == companyId.Value);
            if (companyBid?.Status == "Withdrawn")
            {
                var wasWinner = quotation.Status == "Finalized" && companyBid.IsWinner;
                if (!wasWinner)
                    throw new UnauthorizedAccessException(
                        "External company's bid has been withdrawn from this quotation");
            }

            // M4: if quotation itself is in a terminal status, only the winner (if finalized) may view
            var terminalStatuses = new[] { "Cancelled" };
            if (terminalStatuses.Contains(quotation.Status))
                throw new UnauthorizedAccessException(
                    $"External company cannot view a quotation in terminal status '{quotation.Status}'");

            return;
        }

        throw new UnauthorizedAccessException("Insufficient permissions to view this quotation");
    }

    /// <summary>
    /// Ensures the caller is an ExtAdmin user belonging to the specified company.
    /// Used for decline / submit / respond-negotiation paths.
    /// </summary>
    public static void EnsureExtCompanyUser(ICurrentUserService user, Guid expectedCompanyId)
    {
        if (!user.IsInRole("ExtAdmin"))
            throw new UnauthorizedAccessException("Only external company users can perform this operation");

        var companyId = user.CompanyId;
        if (!companyId.HasValue)
            throw new UnauthorizedAccessException("External company user has no company_id claim");

        if (companyId.Value != expectedCompanyId)
            throw new UnauthorizedAccessException(
                "External company can only perform this operation for their own company");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Checker role helper
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the current user has the ExtAppraisalChecker role.
    /// </summary>
    public static bool IsChecker(ICurrentUserService user)
    {
        return user.IsInRole("ExtAppraisalChecker");
    }

    /// <summary>
    /// Ensures the ext-company caller can submit a quotation for the given invitation.
    /// Accepts either Maker (ExtAdmin) or Checker (ExtAppraisalChecker) — both must belong to the invited company.
    /// </summary>
    public static void EnsureCanSubmitQuotation(
        QuotationInvitation invitation,
        ICurrentUserService user)
    {
        if (!user.IsInRole("ExtAdmin") && !user.IsInRole("ExtAppraisalChecker"))
            throw new UnauthorizedAccessException("Only external company users can submit quotations");

        var companyId = user.CompanyId;
        if (!companyId.HasValue)
            throw new UnauthorizedAccessException("External company user has no company_id claim");

        if (invitation.CompanyId != companyId.Value)
            throw new UnauthorizedAccessException(
                "External company can only submit quotations for their own invitation");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // View filtering helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Filters the company quotation collection based on the caller's role:
    ///   Admin: all quotations returned unchanged.
    ///   RM: only shortlisted quotations.
    ///   ExtAdmin (Maker) / ExtAppraisalChecker (Checker): only the quotation belonging to their company.
    /// </summary>
    public static IEnumerable<CompanyQuotation> FilterCompanyQuotationsForView(
        IEnumerable<CompanyQuotation> quotations,
        ICurrentUserService user)
    {
        if (user.IsInRole("Admin") || user.IsInRole("IntAdmin"))
            return quotations;

        if (user.IsInRole("RequestMaker"))
            return quotations.Where(q => q.IsShortlisted);

        // Both Maker and Checker belong to the same ext company; Checker must see the Maker's draft.
        if (user.IsInRole("ExtAdmin") || user.IsInRole("ExtAppraisalChecker"))
        {
            var companyId = user.CompanyId;
            if (!companyId.HasValue) return [];
            return quotations.Where(q => q.CompanyId == companyId.Value);
        }

        return [];
    }

    /// <summary>
    /// Whether the caller may see the full list of invited companies (names) on a quotation.
    /// Internal users (Admin / IntAdmin / RequestMaker) may; external company users may not,
    /// because disclosing rival invitees during a competitive bid is a privacy concern.
    /// </summary>
    public static bool CanViewInvitedCompanies(ICurrentUserService user)
    {
        return user.IsInRole("Admin")
               || user.IsInRole("IntAdmin")
               || user.IsInRole("RequestMaker");
    }
}