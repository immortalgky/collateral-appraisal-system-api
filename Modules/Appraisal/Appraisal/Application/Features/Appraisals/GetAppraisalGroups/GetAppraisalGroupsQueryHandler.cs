using Appraisal.Application.Features.Appraisals.GetAppraisals;
using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Application.Features.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Exceptions;
using Shared.Identity;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalGroups;

/// <summary>
/// Group counts for the appraisal search. The WHERE clause, free-text search, address match and company scope
/// all come from <see cref="AppraisalFilterBuilder.BuildFilter"/>, the same call the list makes, so the counts
/// always agree with the rows the list would return for the same filters.
///
/// Like the list's count it reads <c>appraisal.Appraisals</c> unless a predicate or the grouping column needs
/// something only the view has (<see cref="AppraisalFilterSql.RequiresView"/>).
/// </summary>
public class GetAppraisalGroupsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IAddressNameSearch addressNameSearch
) : IQueryHandler<GetAppraisalGroupsQuery, GetAppraisalGroupsResult>
{
    /// <summary>
    /// The only groupings, as the column each one reads. Never built from the caller's text: <c>groupBy</c> picks
    /// an entry, and the column interpolated into SQL is the entry's literal. <c>NeedsView</c> is true for
    /// columns that exist only on the view (latest assignment, first land location).
    /// </summary>
    private static readonly Dictionary<string, (string Column, bool NeedsView)> Groupings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = ("Status", false),
            ["slaStatus"] = ("SLAStatus", false),
            ["priority"] = ("Priority", false),
            ["province"] = ("Province", true),
            ["assignmentType"] = ("AssignmentType", true),
        };

    internal static (string Column, bool NeedsView) ResolveGrouping(string? groupBy) =>
        groupBy is not null && Groupings.TryGetValue(groupBy.Trim(), out var grouping)
            ? grouping
            : throw new BadRequestException(
                "groupBy is required and must be one of: " + string.Join(", ", Groupings.Keys) + ".");

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "Nothing user-supplied is interpolated. column is a literal from the Groupings whitelist; ViewFrom, " +
            "BaseTableFrom and WhereClause are assembled by AppraisalFilterBuilder from string literals with every " +
            "value bound as a @parameter; SearchQueryHint is one of two literals.")]
    internal static string BuildSql(string column, bool needsView, AppraisalFilterSql filter)
    {
        var (from, where) = filter.RequiresView || needsView
            ? (filter.ViewFrom, filter.WhereClause)
            : (filter.BaseTableFrom, filter.BaseTableWhereClause);

        // The key is normalised in SQL so NULL and '' land in ONE group instead of two that both read "".
        var key = $"ISNULL(NULLIF({column}, N''), N'')";
        return $"""
            SELECT {key} AS [Key],
                   COUNT(*) AS [Count],
                   SUM(CASE WHEN SLAStatus = 'Breached' THEN 1 ELSE 0 END) AS Breached,
                   SUM(CASE WHEN SLAStatus = 'AtRisk' THEN 1 ELSE 0 END) AS AtRisk
            FROM {from}{where}
            GROUP BY {key}
            ORDER BY COUNT(*) DESC, {key}{filter.SearchQueryHint}
            """;
    }

    public async Task<GetAppraisalGroupsResult> Handle(
        GetAppraisalGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var (column, needsView) = ResolveGrouping(query.GroupBy);

        var addressMatch = await addressNameSearch.MatchAsync(query.Filter?.Search, cancellationToken);
        var filter = AppraisalFilterBuilder.BuildFilter(
            query.Filter, AppraisalAccessScope.GetEnforcedCompanyId(currentUser), addressMatch: addressMatch);

        var sql = BuildSql(column, needsView, filter);

        var connection = connectionFactory.GetOpenConnection();
        var rows = (await connection.QueryAsync<AppraisalGroupRow>(
            new CommandDefinition(sql, filter.Parameters, cancellationToken: cancellationToken))).ToList();

        return new GetAppraisalGroupsResult(rows, rows.Sum(r => r.Count));
    }
}
