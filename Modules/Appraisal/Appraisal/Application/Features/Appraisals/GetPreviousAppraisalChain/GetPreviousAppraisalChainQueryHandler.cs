using Appraisal.Application.Features.Shared;
using Dapper;
using Shared.Data;
using Shared.Identity;

namespace Appraisal.Application.Features.Appraisals.GetPreviousAppraisalChain;

/// <summary>
/// Walks the ancestor chain of an appraisal: appraisal.Appraisals.Id ->
/// appraisal.Appraisals.PrevAppraisalId -> that appraisal -> repeat. Returns the chain
/// nearest-ancestor-first; the queried appraisal itself is excluded.
///
/// When the oldest CAS appraisal was itself reappraised from a legacy AS400 book (PrevAppraisalNumber,
/// no PrevAppraisalId), that book is appended as the final item with no AppraisalId — so the chain
/// does not look broken at the CAS boundary. Its value/date come from the bank's legacy listing.
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
        // The legacy book belongs to no company, so an external caller never sees it.
        var legacyArm = """

            UNION ALL

            SELECT CAST(NULL AS uniqueidentifier), c.PrevAppraisalNumber,
                   CAST(l.ValuationDate AS datetime2), l.ValuationPriceInBaht, CAST(NULL AS nvarchar(50)),
                   c.Depth + 1
            FROM chain c
            -- The book's latest valuation — the appraisal just before this chain, as the candidate list
            -- dates it (appraisal.vw_LegacyBookLatestValuation, the one definition).
            OUTER APPLY (
                SELECT lv.ValuationDate, lv.ValuationPriceInBaht
                FROM appraisal.vw_LegacyBookLatestValuation lv
                WHERE lv.BookNumber = c.PrevAppraisalNumber
            ) l
            WHERE c.PrevAppraisalId IS NULL
              AND c.PrevAppraisalNumber IS NOT NULL
            """;
        if (enforcedCompanyId.HasValue)
        {
            accessJoin = """

                JOIN appraisal.vw_AppraisalList al
                  ON al.Id = c.Id
                 AND TRY_CAST(al.AssigneeCompanyId AS uniqueidentifier) = @CompanyId
                """;
            parameters.Add("CompanyId", enforcedCompanyId.Value);
            legacyArm = string.Empty;
        }

        // ── Recursive CTE: walks appraisal.Appraisals.PrevAppraisalId up to the chain root.
        // Depth 1 is the queried appraisal itself (the anchor row), excluded by "WHERE c.Depth > 1".
        //
        // The cycle guard is "CHARINDEX(..., c.Path) = 0", NOT a depth limit. The previous version
        // used "Depth < 20" together with OPTION (MAXRECURSION 20), which is broken: the Depth
        // predicate stops the recursion first, so MAXRECURSION never fires. There is no error — a
        // chain longer than 20 is silently truncated and the 20th ancestor is returned as if it were
        // the root. Chains beyond 20 are reachable in practice through construction inspections
        // (purpose 06/11), which can run to dozens per project.
        //
        // Uses appraisal.Appraisals.PrevAppraisalId as the chain link (confirmed by the product owner).
        //
        // NOTE: that column is written ONCE at appraisal creation (Appraisal.cs:117) and is never
        // re-synced when the request is edited later — UpdateRequestCommandHandler only writes the
        // request side. The team's chosen answer is to BLOCK editing PrevAppraisalId once an
        // appraisal exists, rather than to sync it. If editing is ever re-enabled, this query and
        // vw_RegulatoryExport must both be revisited together.
        //
        // Must use the same column as vw_RegulatoryExport, or the screen and the report would show
        // different chains.
        //
        // vw_AppraisalCopyTemplate.AppraisalValue comes from appraisal.ValuationAnalyses, which
        // carries a unique index on AppraisalId (1:1 with Appraisal) — no fan-out risk.
        // .AppraisalDate is the valuation date (ValuationDate, appointment as fallback), not the
        // raw appointment slot.
        var sql = $"""
            WITH chain AS (
                SELECT a.Id, a.PrevAppraisalId, a.PrevAppraisalNumber, 1 AS Depth,
                       CAST('|' + CAST(a.Id AS varchar(36)) + '|' AS varchar(max)) AS Path
                FROM appraisal.Appraisals a
                WHERE a.Id = @AppraisalId AND a.IsDeleted = 0

                UNION ALL

                SELECT p.Id, p.PrevAppraisalId, p.PrevAppraisalNumber, c.Depth + 1,
                       CAST(c.Path + CAST(p.Id AS varchar(36)) + '|' AS varchar(max))
                FROM chain c
                JOIN appraisal.Appraisals p ON p.Id = c.PrevAppraisalId AND p.IsDeleted = 0
                WHERE CHARINDEX('|' + CAST(p.Id AS varchar(36)) + '|', c.Path) = 0
            )
            SELECT
                v.AppraisalId,
                v.AppraisalNumber,
                v.AppraisalDate,
                v.AppraisalValue,
                v.Status,
                c.Depth
            FROM chain c
            JOIN appraisal.vw_AppraisalCopyTemplate v ON v.AppraisalId = c.Id{accessJoin}
            WHERE c.Depth > 1{legacyArm}
            ORDER BY Depth
            OPTION (MAXRECURSION 0)
            """;

        var items = (await connectionFactory.QueryAsync<PreviousAppraisalDto>(sql, parameters)).ToList();

        return new GetPreviousAppraisalChainResult(items);
    }
}
