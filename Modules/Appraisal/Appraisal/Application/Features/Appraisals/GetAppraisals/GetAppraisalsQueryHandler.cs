using Appraisal.Application.Features.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;
using Workflow.Contracts.Sla;
using Shared.Time;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

/// <summary>
/// Handler for getting all Appraisals with pagination, filtering, sorting, and facets.
/// Uses SQL view + Dapper for efficient read queries.
///
/// The view is expensive per row (it resolves the latest assignment, the first land location, the
/// customer and the latest appointment for each appraisal), so every statement here is shaped to
/// touch as few rows of it as possible:
///   • the page is resolved as a list of Ids first, then only those Ids are enriched;
///   • the total count runs off appraisal.Appraisals whenever the filter allows it;
///   • the status facet is a GROUP BY in SQL, not every matching row pulled into memory.
/// </summary>
public class GetAppraisalsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IBusinessTimeCalculator businessTime,
    IDateTimeProvider clock
) : IQueryHandler<GetAppraisalsQuery, GetAppraisalsResult>
{
    private const string View = "appraisal.vw_AppraisalList";
    private const string BaseTable = "appraisal.Appraisals";

    /// <summary>
    /// Upper bound on rows per page. The enrichment step binds one parameter per id and SQL Server
    /// caps a statement at 2100 parameters, so an unbounded page size is a 500 waiting to happen
    /// (reproduced: pageSize=2000 succeeds, 2099 does not). 200 matches the ceiling the audit-log
    /// and access-matrix reports already use; the SPA never asks for more than 100, and bulk
    /// extraction is what /appraisals/export is for.
    /// </summary>
    private const int MaxPageSize = 200;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "Nothing user-supplied is interpolated. View is a const; WhereClause is assembled by " +
            "AppraisalFilterBuilder from string literals with every value bound as a @parameter; " +
            "orderBy comes from BuildOrderBy, which only emits a column from AllowedSortFields plus " +
            "ASC/DESC and is additionally checked by DapperPaginationExtensions.ValidateOrderBy on " +
            "the call above. See AppraisalFilterBuilderTests for the pinned output.")]
    public async Task<GetAppraisalsResult> Handle(
        GetAppraisalsQuery query,
        CancellationToken cancellationToken)
    {
        var filter = query.Filter;
        var enforcedCompanyId = AppraisalAccessScope.GetEnforcedCompanyId(currentUser);
        var sqlFilter = AppraisalFilterBuilder.BuildFilter(filter, enforcedCompanyId);
        var orderBy = AppraisalFilterBuilder.BuildOrderBy(filter);

        // Page the KEYS, not the rows. Selecting only Id lets the optimizer drop every OUTER APPLY
        // the filter and sort do not actually read, so sorting 100k+ appraisals costs a fraction of
        // what it costs to project the full view and then throw all but one page away. It also
        // makes deep pages (OFFSET 10000) cheap, because the discarded rows are never enriched.
        // Clamped rather than rejected so an oversized request still returns data; callers read the
        // effective size back off PaginatedResult.PageSize.
        var pagination = query.PaginationRequest with
        {
            PageSize = Math.Clamp(query.PaginationRequest.PageSize, 1, MaxPageSize)
        };

        var idPage = await connectionFactory.QueryPaginatedAsync<Guid>(
            $"SELECT Id FROM {View}{sqlFilter.WhereClause}",
            BuildCountSql(sqlFilter),
            orderBy,
            pagination,
            sqlFilter.Parameters,
            // Free text expands to a 17-way UNION of LIKE arms. Without a per-execution compile the
            // optimizer plans them for an unknown pattern — i.e. a possible leading wildcard — and
            // scans. See AppraisalFilterBuilder for the measurements.
            recompile: sqlFilter.HasFreeTextSearch);

        var connection = connectionFactory.GetOpenConnection();

        // Enrich just this page. IN (@Ids) does not preserve order, so the sort is repeated here.
        // orderBy is safe to interpolate: BuildOrderBy only ever emits a whitelisted column plus
        // ASC/DESC, and QueryPaginatedAsync above has already run it through ValidateOrderBy — a
        // rejected clause throws before this line is reached.
        var ids = idPage.Items.ToList();
        var rows = ids.Count == 0
            ? []
            : (await connection.QueryAsync<AppraisalDto>(new CommandDefinition(
                $"SELECT * FROM {View} WHERE Id IN @Ids ORDER BY {orderBy}",
                new { Ids = ids },
                cancellationToken: cancellationToken))).ToList();

        // Business-time Elapsed/Remaining: exclude weekends, holidays and lunch via the shared
        // calculator. Only the returned page is recomputed; the calculator caches config/holidays.
        // Elapsed runs from CreatedAt; Remaining runs to SLADueDate.
        var now = clock.ApplicationNow;
        var items = new List<AppraisalDto>(rows.Count);
        foreach (var a in rows)
        {
            var (elapsed, remaining) =
                await businessTime.ComputeElapsedRemainingHoursAsync(now, a.CreatedAt, a.SLADueDate, ct: cancellationToken);
            items.Add(a with { ElapsedHours = elapsed, RemainingHours = remaining });
        }

        var pagedResult = new PaginatedResult<AppraisalDto>(items, idPage.Count, idPage.PageNumber, idPage.PageSize);

        var facets = await BuildStatusFacetsAsync(filter, enforcedCompanyId, cancellationToken);

        return new GetAppraisalsResult(pagedResult, facets);
    }

    /// <summary>
    /// Counts off <c>appraisal.Appraisals</c> when no predicate needs a column only the view has.
    /// Returning <c>null</c> lets <see cref="DapperPaginationExtensions"/> fall back to wrapping the
    /// Id query in <c>SELECT COUNT(*) FROM (…)</c>.
    /// Must stay <c>COUNT(*)</c>, not <c>COUNT_BIG(*)</c> — the helper reads the scalar as an int.
    /// </summary>
    private static string? BuildCountSql(AppraisalFilterSql sqlFilter) =>
        sqlFilter.RequiresView
            ? null
            : $"SELECT COUNT(*) FROM {BaseTable}{sqlFilter.BaseTableWhereClause}";

    /// <summary>
    /// Counts appraisals per status for the filter chips above the results table.
    ///
    /// Two deliberate differences from the original implementation:
    ///   • the grouping happens in SQL. It used to select every matching row (all ~100k of them on
    ///     an unfiltered list) and group them in memory, once per page load.
    ///   • the Status predicate is excluded from the WHERE. Counting statuses through a clause that
    ///     already pins one status can only ever return that one chip, which left the user unable to
    ///     switch status from the chip row. Every other active filter still narrows the counts.
    ///
    /// Only Status is populated. The other four groups are part of the response contract but no
    /// client reads them, and AssignmentType in particular costs more than the rest of the request
    /// put together because it has to resolve the latest assignment for every matching appraisal.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "source is one of two private consts and where is assembled by AppraisalFilterBuilder " +
            "from string literals with every value bound as a @parameter — no user input reaches " +
            "the command text.")]
    private async Task<AppraisalFacets> BuildStatusFacetsAsync(
        GetAppraisalsFilterRequest? filter,
        Guid? enforcedCompanyId,
        CancellationToken cancellationToken)
    {
        var facetFilter = AppraisalFilterBuilder.BuildFilter(filter, enforcedCompanyId, excludeStatus: true);
        var source = facetFilter.RequiresView ? View : BaseTable;
        var where = facetFilter.RequiresView ? facetFilter.WhereClause : facetFilter.BaseTableWhereClause;

        // ORDER BY count first to match the previous ordering, then by value so that chips do not
        // reshuffle between requests when counts tie.
        var sql = $"""
            SELECT Status AS Value, COUNT(*) AS [Count]
            FROM {source}{where}
            GROUP BY Status
            ORDER BY COUNT(*) DESC, Status ASC
            """ + (facetFilter.HasFreeTextSearch ? "\nOPTION (RECOMPILE)" : "");

        // On the external-company path this always reads the view, so it is worth abandoning when
        // the caller navigates away.
        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<FacetRow>(new CommandDefinition(
            sql, facetFilter.Parameters, cancellationToken: cancellationToken));

        return new AppraisalFacets
        {
            Status = rows.Select(r => new FacetItem(r.Value, r.Count)).ToList()
        };
    }

    /// <summary>
    /// A mutable class rather than a positional record, so Dapper binds these by NAME.
    /// <see cref="FacetItem"/> is positional and would bind by position instead.
    /// </summary>
    private sealed class FacetRow
    {
        public string Value { get; set; } = "";
        public int Count { get; set; } = 0;
    }
}
