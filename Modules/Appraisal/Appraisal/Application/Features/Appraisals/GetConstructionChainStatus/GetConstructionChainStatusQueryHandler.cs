using Appraisal.Application.Features.Appraisals.ResolveLatestInAppraisalChain;
using Appraisal.Application.Features.Shared;
using Appraisal.Contracts.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Dapper;
using Shared.Identity;

namespace Appraisal.Application.Features.Appraisals.GetConstructionChainStatus;

/// <summary>
/// Answers <see cref="GetConstructionChainStatusQuery"/> over the whole appraisal tree (the same walk
/// <see cref="ResolveLatestInAppraisalChainQueryHandler"/> uses). The data is loaded here; the rules live in
/// <see cref="ConstructionChainStatusRules"/> so they can be tested without a database.
/// </summary>
public class GetConstructionChainStatusQueryHandler(
    AppraisalDbContext dbContext,
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser
) : IRequestHandler<GetConstructionChainStatusQuery, ConstructionChainStatusResult?>
{
    // A submitted, unfinished construction-inspection request on the tree. Cross-module read in the same
    // style as AppraisalSearchPredicate: request.Requests is queried by SQL, never through its DbContext.
    //
    // "Submitted" is RequestedAt, stamped by Request.Submit and never cleared; a draft or "New" request has
    // none. A request whose appraisal is already Completed/Cancelled/deleted is finished. One whose
    // appraisal has not been created yet (the window between submit and the workflow creating it) counts
    // as open.
    //
    // The request being validated is excluded (@ExcludeRequestId; Guid.Empty = none): a submit replay would
    // otherwise find itself as "already inspecting this chain".
    //
    // Purposes 06 and 11 are both AppraisalType Progressive, so they form one group.
    //
    // A request whose prior is a block is skipped: every unit of the block points its first inspection there,
    // so it cannot be attributed to the unit being checked.
    private const string OpenRequestSql = """
        SELECT TOP 1 r.Id AS RequestId, r.RequestNumber
        FROM request.Requests r
        JOIN request.RequestDetails d ON d.RequestId = r.Id
        LEFT JOIN appraisal.Appraisals a ON a.RequestId = r.Id
        WHERE r.IsDeleted = 0
          AND r.RequestedAt IS NOT NULL
          AND r.Status NOT IN ('Completed', 'Cancelled')
          AND r.Purpose IN ('06', '11')
          AND d.PrevAppraisalId IN @TreeIds
          AND NOT EXISTS (SELECT 1 FROM appraisal.Projects p WHERE p.AppraisalId = d.PrevAppraisalId)
          AND r.Id <> @ExcludeRequestId
          AND (a.Id IS NULL OR (a.IsDeleted = 0 AND a.Status NOT IN ('Completed', 'Cancelled')))
        ORDER BY r.RequestedAt DESC, r.Id DESC
        """;

    private const string ProgressSql = """
        SELECT AppraisalId, CurrentProgressPct, IsUnderConstruction
        FROM appraisal.vw_AppraisalConstructionProgress
        WHERE AppraisalId IN @AppraisalIds
        """;

    public async Task<ConstructionChainStatusResult?> Handle(
        GetConstructionChainStatusQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();

        // An external (company) caller only reads appraisals assigned to its company.
        await AppraisalAccessScope.EnsureCallerMayReadAsync(
            connection, currentUser, query.AppraisalId, cancellationToken);

        var treeIds = await AppraisalChainTree.LoadAsync(dbContext, query.AppraisalId, cancellationToken);
        if (treeIds is null)
            return null;

        // Hoisted so EF parameterises the code. Only completed appraisals matter: cancelled ones are out,
        // and an in-flight one is the open-request check's business. Same ordering as the chain resolver.
        var completedStatus = AppraisalStatus.Completed.Code;
        var completed = await dbContext.Appraisals
            .AsNoTracking()
            .Where(a => treeIds.Contains(a.Id) && a.Status.Code == completedStatus && a.CompletedAt != null)
            .OrderByDescending(a => a.CompletedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new ChainAppraisal(
                a.Id, a.AppraisalNumber, a.AppraisalType, a.Purpose, a.InspectionNumber, a.CompletedAt!.Value))
            .ToListAsync(cancellationToken);

        // One read for the completed appraisals and the pick (which may not be completed itself).
        var progress = (await connection.QueryAsync<ProgressRow>(new CommandDefinition(
                ProgressSql,
                new { AppraisalIds = completed.Select(c => c.Id).Append(query.AppraisalId).Distinct().ToArray() },
                cancellationToken: cancellationToken)))
            .ToDictionary(p => p.AppraisalId, p => new ChainProgress(p.CurrentProgressPct, p.IsUnderConstruction));

        var openRequest = await connection.QueryFirstOrDefaultAsync<OpenInspectionRequestRef>(new CommandDefinition(
            OpenRequestSql,
            new { TreeIds = treeIds.ToArray(), ExcludeRequestId = query.ExcludeRequestId ?? Guid.Empty },
            cancellationToken: cancellationToken));

        return ConstructionChainStatusRules.Evaluate(query.AppraisalId, completed, progress, openRequest);
    }

    private sealed record ProgressRow(Guid AppraisalId, decimal CurrentProgressPct, bool IsUnderConstruction);
}
