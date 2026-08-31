using Appraisal.Application.Features.Appraisals.Shared;
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
/// Handler for getting all Appraisals with pagination, filtering and sorting.
/// Uses SQL view + Dapper for efficient read queries.
///
/// The view is expensive per row (it resolves the latest assignment, the first land location, the
/// customer and the latest appointment for each appraisal), so every statement here is shaped to
/// touch as few rows of it as possible:
///   • the page is resolved as a list of Ids first, then only those Ids are enriched;
///   • the total count runs off appraisal.Appraisals whenever the filter allows it;
/// </summary>
public class GetAppraisalsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IBusinessTimeCalculator businessTime,
    IDateTimeProvider clock,
    IAddressNameSearch addressNameSearch
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
        // Resolved once and reused by both the page and the count, so a search that does name an
        // address does not probe the masters twice.
        var addressMatch = await addressNameSearch.MatchAsync(filter?.Search, cancellationToken);
        var sqlFilter = AppraisalFilterBuilder.BuildFilter(filter, enforcedCompanyId, addressMatch: addressMatch);
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

// Facets are no longer computed. Nothing renders them once app#357 lands, and the count
        // was never free: it GROUP BYs the whole matching set, so unlike the page — which resolves
        // 25 ids and enriches only those — it cannot stop early.
        //
        // Measured end to end against origin/main on the same database, ten interleaved runs each,
        // median wall clock for GET /appraisals:
        //
        //   open the list         71ms -> 59ms     filter by province   223ms -> 136ms
        //   filter by status     195ms -> 177ms    search by customer   780ms -> 519ms
        //                                          company + province  1022ms -> 548ms
        //
        // Note this is the whole request, not one statement. On the view path the facet's own CPU
        // is roughly the same as the count's and the page's, so it is about a third of the DB work
        // rather than an outlier — the earlier version of this comment quoted a view-path facet
        // against base-path page queries, which flattered it.
        //
        // ⚠ The property stays on the contract, always null. Frontend main still reads it and
        //   renders the chip row behind a `facets && facets.status.length > 0` guard, so deploying
        //   this first degrades quietly (the chips disappear; the status dropdown still filters)
        //   rather than breaking. Land app#357 first to avoid even that.
        return new GetAppraisalsResult(pagedResult);
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

}
