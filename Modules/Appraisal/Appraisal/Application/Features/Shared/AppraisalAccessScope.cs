using System.Data;
using Appraisal.Domain.Appraisals.Exceptions;
using Dapper;
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

    // For an external (company) caller: only appraisals assigned to their company, as the sibling appraisal
    // reads do (GetPreviousAppraisalChain).
    private const string CompanyScopeSql = """
        SELECT CAST(CASE WHEN EXISTS (
                   SELECT 1 FROM appraisal.vw_AppraisalList al
                   WHERE al.Id = @AppraisalId
                     AND TRY_CAST(al.AssigneeCompanyId AS uniqueidentifier) = @CompanyId)
               THEN 1 ELSE 0 END AS bit)
        """;

    /// <summary>
    /// 404 for an external (company) caller on an appraisal not assigned to its company, so existence is never
    /// confirmed. Internal callers (no company) pass without a query.
    /// </summary>
    public static async Task EnsureCallerMayReadAsync(
        IDbConnection connection, ICurrentUserService user, Guid appraisalId, CancellationToken cancellationToken)
    {
        if (GetEnforcedCompanyId(user) is { } companyId
            && !await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                CompanyScopeSql, new { AppraisalId = appraisalId, CompanyId = companyId },
                cancellationToken: cancellationToken)))
            throw new AppraisalNotFoundException(appraisalId);
    }
}
