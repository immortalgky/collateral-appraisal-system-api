using Shared.Identity;

namespace Appraisal.Application.Features.Shared;

/// <summary>
/// Single place that decides whether a query handler should force a company-scope filter.
/// Bank/internal callers have no <c>company_id</c> claim and see everything; external
/// valuation-company callers have a <c>company_id</c> and are scoped to their own company.
///
/// Mirrors the per-feature policy pattern used by <c>QuotationAccessPolicy</c>.
/// </summary>
public static class AppraisalAccessScope
{
    /// <summary>
    /// Returns the company id to force into list/search queries, or <c>null</c> when the
    /// caller is internal (bank) and may see all rows.
    /// </summary>
    public static Guid? GetEnforcedCompanyId(ICurrentUserService user) => user.CompanyId;
}
