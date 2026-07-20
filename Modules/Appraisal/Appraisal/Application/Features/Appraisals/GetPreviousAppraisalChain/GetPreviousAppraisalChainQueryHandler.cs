using Appraisal.Application.Features.Shared;
using Dapper;
using Shared.Data;
using Shared.Identity;

namespace Appraisal.Application.Features.Appraisals.GetPreviousAppraisalChain;

/// <summary>
/// Walks the ancestor chain of an appraisal: appraisal.Appraisals.Id -> RequestId ->
/// request.RequestDetails.PrevAppraisalId -> that appraisal -> repeat. Returns the chain
/// nearest-ancestor-first; the queried appraisal itself is excluded.
///
/// Visibility enforcement (enforced server-side in this handler — never trust the client):
///   Internal (bank) callers — <see cref="AppraisalAccessScope.GetEnforcedCompanyId"/> returns
///     null; the full chain is returned.
///   External (company) callers — <c>enforcedCompanyId</c> is set: each ancestor is filtered
///     to those assigned to that company (via vw_AppraisalList.AssigneeCompanyId). Ancestors
///     outside the caller's company are silently dropped rather than causing a 403/404, so the
///     endpoint never confirms the existence of an appraisal the caller cannot see.
/// </summary>
public class GetPreviousAppraisalChainQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser
) : IQueryHandler<GetPreviousAppraisalChainQuery, GetPreviousAppraisalChainResult>
{
    public async Task<GetPreviousAppraisalChainResult> Handle(
        GetPreviousAppraisalChainQuery query,
        CancellationToken cancellationToken)
    {
        var enforcedCompanyId = AppraisalAccessScope.GetEnforcedCompanyId(currentUser);

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", query.AppraisalId);

        // ── Company gate, external callers only ───────────────────────────────
        // vw_AppraisalList is a heavy view (several ROW_NUMBER windows); internal callers
        // never need it, so it is joined in only when there is a company to enforce. Its own
        // joins are all rn=1/TOP 1/1:1, so this cannot fan out the chain.
        var accessJoin = string.Empty;
        if (enforcedCompanyId.HasValue)
        {
            accessJoin = """

                JOIN appraisal.vw_AppraisalList al
                  ON al.Id = c.Id
                 AND TRY_CAST(al.AssigneeCompanyId AS uniqueidentifier) = @CompanyId
                """;
            parameters.Add("CompanyId", enforcedCompanyId.Value);
        }

        // ── Recursive CTE: walk PrevAppraisalId back through the request that raised each
        // prior appraisal. Depth 1 is the queried appraisal itself (anchor row); it is
        // excluded by "WHERE c.Depth > 1" below. "Depth < 20" together with the trailing
        // OPTION (MAXRECURSION 20) is the cycle guard — malformed/looping PrevAppraisalId
        // data must terminate the walk, not hang the query.
        // vw_AppraisalCopyTemplate.AppraisalValue comes from appraisal.ValuationAnalyses,
        // which carries a unique index on AppraisalId (1:1 with Appraisal) — no fan-out risk.
        var sql = $"""
            WITH chain AS (
                SELECT a.Id, a.RequestId, d.PrevAppraisalId, 1 AS Depth
                FROM appraisal.Appraisals a
                JOIN request.RequestDetails d ON d.RequestId = a.RequestId
                WHERE a.Id = @AppraisalId AND a.IsDeleted = 0

                UNION ALL

                SELECT a.Id, a.RequestId, d.PrevAppraisalId, c.Depth + 1
                FROM chain c
                JOIN appraisal.Appraisals a ON a.Id = c.PrevAppraisalId AND a.IsDeleted = 0
                JOIN request.RequestDetails d ON d.RequestId = a.RequestId
                WHERE c.Depth < 20
            )
            SELECT
                v.AppraisalId,
                v.AppraisalNumber,
                v.AppointmentDate AS AppraisalDate,
                v.AppraisalValue,
                v.Status,
                c.Depth
            FROM chain c
            JOIN appraisal.vw_AppraisalCopyTemplate v ON v.AppraisalId = c.Id{accessJoin}
            WHERE c.Depth > 1
            ORDER BY c.Depth
            OPTION (MAXRECURSION 20)
            """;

        var items = (await connectionFactory.QueryAsync<PreviousAppraisalDto>(sql, parameters)).ToList();

        return new GetPreviousAppraisalChainResult(items);
    }
}
