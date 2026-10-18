using Appraisal.Application.Features.Appraisals.GetAppraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalGroups;

/// <summary>
/// Counts of the appraisals <c>GET /appraisals</c> would return for <paramref name="Filter"/>, grouped by one column.
/// </summary>
public record GetAppraisalGroupsQuery(
    string? GroupBy,
    GetAppraisalsFilterRequest? Filter = null
) : IQuery<GetAppraisalGroupsResult>;

/// <param name="Groups">Ordered by <c>Count</c> descending, then <c>Key</c>.</param>
/// <param name="Total">Rows matching the filter, i.e. the sum of the group counts.</param>
public record GetAppraisalGroupsResult(IReadOnlyList<AppraisalGroupRow> Groups, int Total);

/// <param name="Key">The stored value; null or empty is "" (the client labels it).</param>
/// <param name="Breached">Rows in the group whose SLAStatus is 'Breached'.</param>
/// <param name="AtRisk">Rows in the group whose SLAStatus is 'AtRisk'.</param>
public record AppraisalGroupRow(string Key, int Count, int Breached, int AtRisk);
