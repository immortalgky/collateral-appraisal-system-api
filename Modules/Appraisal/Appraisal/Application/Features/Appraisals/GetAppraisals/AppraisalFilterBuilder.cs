using Appraisal.Application.Features.Appraisals.Shared;
using Dapper;
using Shared.Data;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

/// <summary>
/// Shared filter and sort builder for Appraisal list queries.
/// Used by both the paginated list handler and the export handler.
/// </summary>
internal static class AppraisalFilterBuilder
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        // Id is NOT here on purpose: BuildOrderBy appends it as the tiebreaker, and a sort field
        // that repeats it makes SQL Server reject the whole ORDER BY.
        "AppraisalNumber", "RequestNumber", "CustomerName", "Status", "AppraisalType",
        "Priority", "SLADueDate", "SLAStatus", "CreatedAt", "AssignedDate",
        "AppointmentDateTime", "Province", "District", "SubDistrict", "Channel", "BankingSegment",
        "FacilityLimit", "PropertyCount", "ElapsedHours", "RemainingHours",
        "AssignmentType", "CompanyName", "RequestedAt", "Purpose"
    };

    /// <param name="addressMatch">
    /// Which address levels <c>filter.Search</c> names, from <see cref="IAddressNameSearch"/>.
    /// Left at its default the six address arms are dropped, which is what every caller that does
    /// not offer address-name search wants.
    /// </param>
    public static AppraisalFilterSql BuildFilter(
        GetAppraisalsFilterRequest? filter,
        Guid? enforcedCompanyId = null,
        AddressNameMatch addressMatch = default)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // Set whenever a predicate reads a column that appraisal.Appraisals does not have — i.e.
        // one the view synthesises (latest assignment, first land location, customer, appointment).
        // While this stays false the caller may count and page straight off the base table.
        var requiresView = false;

        // See AppraisalFilterSql.HasFreeTextSearch for why this is tracked.
        var hasFreeTextSearch = false;

        // The free-text predicate is NOT a condition: it is a derived table the caller joins from
        // the front. See AppraisalFilterSql.ViewFrom.
        var searchSource = "";

        // External (company) callers are always scoped to their own company; the caller-supplied
        // AssigneeCompanyId on the filter is ignored to prevent cross-company peeking.
        // AppraisalAssignments.AssigneeCompanyId is nvarchar(100), so bind a string — passing a
        // Guid forces SQL to TRY_CAST every column value to uniqueidentifier, which throws on
        // rows that hold non-GUID text.
        if (enforcedCompanyId.HasValue)
        {
            conditions.Add("AssigneeCompanyId = @ScopedCompanyId");
            parameters.Add("ScopedCompanyId", enforcedCompanyId.Value.ToString());
            requiresView = true;
        }

        if (filter is not null)
        {
            // Free-text search. Shared with the navbar quick-search via AppraisalSearchPredicate so
            // the two boxes can never find different things for the same term.
            //
            // This used to be three leading-wildcard LIKEs OR'ed over the view's own columns. Two of
            // them (CustomerName, RequestNumber) are produced by the view's APPLYs, so the APPLYs had
            // to run for every row before the predicate could reject anything, and the leading
            // wildcard meant no index could seek. It also only ever looked at three columns — a
            // title deed, an LOS number or a phone number found nothing.
            //
            // The replacement is a join to base tables only, so the matching ids are resolved
            // before the view does any work and requiresView stays false: the count runs off
            // appraisal.Appraisals.
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = AppraisalSearchPredicate.BuildIdSource(filter.Search, address: addressMatch);
                if (search is null)
                {
                    // Shorter than the minimum useful term. Match nothing rather than everything —
                    // silently ignoring the box would show an unfiltered list that looks filtered.
                    conditions.Add("1 = 0");
                }
                else
                {
                    searchSource = search.Value.Sql;
                    parameters.AddDynamicParams(search.Value.Parameters);
                    hasFreeTextSearch = true;
                }
            }

            // Multi-value filters (comma-separated -> IN clause). A single value still emits
            // `Column = @Param`, so links and saved searches written before the filter bar could
            // select more than one value keep producing exactly the same SQL.
            AddMultiValueFilter(conditions, parameters, filter.Status, "Status", "@Statuses");
            AddMultiValueFilter(conditions, parameters, filter.Priority, "Priority", "@Priorities");
            AddMultiValueFilter(conditions, parameters, filter.AppraisalType, "AppraisalType", "@AppraisalTypes");
            AddMultiValueFilter(conditions, parameters, filter.SlaStatus, "SLAStatus", "@SlaStatuses");
            if (AddMultiValueFilter(conditions, parameters, filter.AssignmentType, "AssignmentType",
                    "@AssignmentTypes"))
                requiresView = true;
            AddMultiValueFilter(conditions, parameters, filter.Purpose, "Purpose", "@Purposes");
            AddPropertyTypeFilter(conditions, parameters, filter.PropertyType);

            // Exact match filters
            if (!string.IsNullOrWhiteSpace(filter.AssigneeUserId))
            {
                conditions.Add("AssigneeUserId = @AssigneeUserId");
                parameters.Add("AssigneeUserId", filter.AssigneeUserId);
                requiresView = true;
            }

            if (!enforcedCompanyId.HasValue &&
                AddMultiValueFilter(conditions, parameters, filter.AssigneeCompanyId, "AssigneeCompanyId",
                    "@AssigneeCompanyIds"))
                requiresView = true;

            if (!string.IsNullOrWhiteSpace(filter.Channel))
            {
                conditions.Add("Channel = @Channel");
                parameters.Add("Channel", filter.Channel);
            }

            AddMultiValueFilter(conditions, parameters, filter.BankingSegment, "BankingSegment",
                "@BankingSegments");

            if (filter.IsPma.HasValue)
            {
                conditions.Add("IsPma = @IsPma");
                parameters.Add("IsPma", filter.IsPma.Value);
            }

            // Geographic filters
            if (AddMultiValueFilter(conditions, parameters, filter.Province, "Province", "@Provinces"))
                requiresView = true;

            if (!string.IsNullOrWhiteSpace(filter.District))
            {
                conditions.Add("District = @District");
                parameters.Add("District", filter.District);
                requiresView = true;
            }

            // Date range filters
            AddDateRangeFilter(conditions, parameters, filter.CreatedFrom, filter.CreatedTo,
                "CreatedAt", "CreatedFrom", "CreatedTo");

            AddDateRangeFilter(conditions, parameters, filter.SlaDueDateFrom, filter.SlaDueDateTo,
                "SLADueDate", "SlaDueDateFrom", "SlaDueDateTo");

            if (AddDateRangeFilter(conditions, parameters, filter.AssignedDateFrom, filter.AssignedDateTo,
                    "AssignedDate", "AssignedDateFrom", "AssignedDateTo"))
                requiresView = true;

            if (AddDateRangeFilter(conditions, parameters, filter.AppointmentDateFrom, filter.AppointmentDateTo,
                    "AppointmentDateTime", "AppointmentDateFrom", "AppointmentDateTo"))
                requiresView = true;

            // Picker-specific additive fields
            // Every customer on the request, not the one the view happens to surface.
            //
            // The view's CustomerName is `OUTER APPLY (SELECT TOP 1 Name ...)` with no ORDER BY, so
            // filtering on it searched an arbitrary single customer per request: on the dev data,
            // pinning the box to "customer name" and typing "Jane" returned 0 of the 22 appraisals
            // that actually carry a Jane, because each of those requests surfaces a John. The
            // all-fields search never had this hole — it reads request.RequestCustomers directly.
            //
            // Written as `RequestId IN (…)`, not `EXISTS`: the WHERE clause is shared between the
            // view (aliased v) and the base table (aliased t), so there is no alias to qualify an
            // outer column with — and RequestCustomers has a RequestId column of its own, which an
            // unqualified correlation would silently bind to, making the predicate always true.
            //
            // It also drops requiresView, so the count runs off appraisal.Appraisals. Measured on
            // the dev database (105,475 appraisals, term "Jane"), page + count:
            //   view column : 579 ms CPU / 337,488 logical reads (RequestCustomers scanned 105,475x)
            //   this        : 130 ms CPU /     921 logical reads (scanned once)
            // The union-shaped search predicate must stay a front-joined derived table for the
            // reasons in AppraisalSearchPredicate.BuildIdSource; a single seekable subquery like
            // this one is not the shape that misbehaves.
            var customerName = StripWidenMarker(filter.CustomerName);
            if (!string.IsNullOrWhiteSpace(customerName))
            {
                conditions.Add(
                    """
                    RequestId IN (SELECT c.RequestId
                                  FROM request.RequestCustomers c
                                  WHERE c.Name LIKE '%' + @CustomerName + '%' ESCAPE '\')
                    """);
                parameters.Add("CustomerName", LikePattern.Escape(customerName));
            }

            var requestNumber = StripWidenMarker(filter.RequestNumber);
            if (!string.IsNullOrWhiteSpace(requestNumber))
            {
                conditions.Add("RequestNumber LIKE '%' + @RequestNumber + '%' ESCAPE '\\'");
                parameters.Add("RequestNumber", LikePattern.Escape(requestNumber));
                // RequestNumber comes from the LEFT JOIN on request.Requests, not the base table.
                requiresView = true;
            }

            var appraisalNumber = StripWidenMarker(filter.AppraisalNumber);
            if (!string.IsNullOrWhiteSpace(appraisalNumber))
            {
                conditions.Add("AppraisalNumber LIKE '%' + @AppraisalNumber + '%' ESCAPE '\\'");
                parameters.Add("AppraisalNumber", LikePattern.Escape(appraisalNumber));
            }

            if (!string.IsNullOrWhiteSpace(filter.SubDistrict))
            {
                // Exact match, like Province and District above: this column holds the 6-digit
                // TIS-1099 geocode the address picker emits, not a Thai name. A substring match
                // crosses provinces — '%1001%' hits both 100101 (Bangkok) and 931001 (Phatthalung).
                conditions.Add("SubDistrict = @SubDistrict");
                parameters.Add("SubDistrict", filter.SubDistrict.Trim());
                requiresView = true;
            }

            AddDateRangeFilter(conditions, parameters, filter.RequestedAtFrom, filter.RequestedAtTo,
                "RequestedAt", "RequestedAtFrom", "RequestedAtTo");
        }

        var whereClause = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
        return new AppraisalFilterSql(whereClause, parameters, requiresView)
        {
            HasFreeTextSearch = hasFreeTextSearch,
            SearchSource = searchSource
        };
    }

    /// <summary>
    /// The ORDER BY for the list, its export and the quotation picker. Always ends with
    /// <c>Id ASC</c>.
    ///
    /// Without a tiebreaker the order of rows that share a sort key is whatever the plan happens
    /// to produce, and these columns are full of ties: rows 90-135 of <c>SLADueDate DESC</c> share
    /// a single value. Measured — the same deep page requested twice came back with different
    /// rows, so a user paging the list could see one row twice and never see another. Id is unique
    /// and deliberately absent from <see cref="AllowedSortFields"/>, so appending it can never
    /// repeat the chosen column, which SQL Server rejects with "a column has been specified more
    /// than once in the order by list".
    /// </summary>
    public static string BuildOrderBy(GetAppraisalsFilterRequest? filter)
    {
        var sortField = AllowedSortFields.Contains(filter?.SortBy ?? "") ? filter!.SortBy! : "CreatedAt";
        var sortDir = string.Equals(filter?.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        // ElapsedHours/RemainingHours are no longer columns on the view (computed in C# via
        // IBusinessTimeCalculator). Their business-time values are monotonic in the underlying
        // timestamps, so translate the sort for exact ordering:
        //   ElapsedHours  ASC  ≡ CreatedAt  DESC (least elapsed = most recently created)
        //   RemainingHours ASC ≡ SLADueDate ASC  (least remaining = earliest due)
        var primary = sortField switch
        {
            "ElapsedHours" => $"CreatedAt {Invert(sortDir)}",
            "RemainingHours" => $"SLADueDate {sortDir}",
            _ => $"{sortField} {sortDir}"
        };

        // Unqualified on purpose: `s` in the search FROM exposes only AppraisalId, so Id resolves
        // to the view (or the base table) in every statement that uses this clause.
        return $"{primary}, Id ASC";
    }

    private static string Invert(string dir) =>
        string.Equals(dir, "ASC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

    /// <summary>
    /// Drops the leading <c>*</c> the free-text box uses to widen a prefix search.
    ///
    /// Only the 'all fields' search is prefix-matched, so only it has anything to widen. These three
    /// pinned filters are already <c>LIKE '%x%'</c> — and because the value is escaped before it
    /// reaches SQL, a <c>*</c> left in place is searched for LITERALLY, so "*somchai" finds nothing
    /// while "somchai" finds the customer. Rather than teach the difference, accept the marker and
    /// ignore it: a user who types it gets what they meant either way.
    ///
    /// A term that is nothing but markers is treated as empty, so "*" alone does not become
    /// <c>LIKE '%%'</c> and return the entire table.
    /// </summary>
    private static string StripWidenMarker(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim().TrimStart('*').Trim();

    /// <returns><c>true</c> when a predicate was actually emitted.</returns>
    private static bool AddMultiValueFilter(
        List<string> conditions, DynamicParameters parameters,
        string? value, string columnName, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0) return false;

        if (values.Length == 1)
        {
            conditions.Add($"{columnName} = {paramName}");
            parameters.Add(paramName.TrimStart('@'), values[0]);
        }
        else
        {
            conditions.Add($"{columnName} IN {paramName}");
            parameters.Add(paramName.TrimStart('@'), values);
        }

        return true;
    }

    /// <summary>
    /// Matches appraisals that carry at least one collateral of the given type(s).
    ///
    /// The type lives in two different places depending on how the appraisal was created, and both
    /// must be searched or block appraisals silently disappear from the results:
    ///   • Normal appraisals → appraisal.AppraisalProperties.PropertyType (N rows per appraisal).
    ///   • Block appraisals  → appraisal.Projects.ProjectType (1:1 with the appraisal). Blocks have
    ///     NO AppraisalProperties rows at all, so the property-side subquery alone misses them.
    /// ProjectType codes ("U"/"LB"/"L") are a subset of the PropertyType codes and share the same
    /// wire format (see Domain/Projects/ProjectType.cs), so no translation is needed.
    /// </summary>
    private static void AddPropertyTypeFilter(
        List<string> conditions, DynamicParameters parameters, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0) return;

        // Written as `Id IN (subquery)` rather than a correlated EXISTS on purpose:
        // AppraisalProperties also has an `Id` column, so inside an EXISTS body an unqualified `Id`
        // would bind to the inner table, and the outer query (SELECT * FROM appraisal.vw_AppraisalList)
        // has no alias to qualify with. On the left of IN, `Id` is unambiguously the outer view's column.
        // The two sources are unioned into one derived table so @PropertyTypes appears exactly
        // once — a repeated list parameter would depend on Dapper expanding every occurrence.
        conditions.Add(
            """
            Id IN (SELECT t.AppraisalId
                   FROM (SELECT ap.AppraisalId, ap.PropertyType AS Code FROM appraisal.AppraisalProperties ap
                         UNION ALL
                         SELECT pr.AppraisalId, pr.ProjectType FROM appraisal.Projects pr) t
                   WHERE t.Code IN @PropertyTypes)
            """);
        parameters.Add("PropertyTypes", values);
    }

    /// <returns><c>true</c> when at least one bound was actually emitted.</returns>
    private static bool AddDateRangeFilter(
        List<string> conditions, DynamicParameters parameters,
        DateTime? from, DateTime? to,
        string columnName, string fromParam, string toParam)
    {
        if (from.HasValue)
        {
            conditions.Add($"{columnName} >= @{fromParam}");
            parameters.Add(fromParam, from.Value);
        }

        if (to.HasValue)
        {
            conditions.Add($"{columnName} < DATEADD(day, 1, @{toParam})");
            parameters.Add(toParam, to.Value);
        }

        return from.HasValue || to.HasValue;
    }
}

/// <summary>
/// A built WHERE clause plus the parameters it references.
/// </summary>
/// <param name="WhereClause">
/// Either empty or already prefixed with <c>" WHERE "</c>, so it can be concatenated onto a
/// <c>SELECT … FROM appraisal.vw_AppraisalList</c> directly.
/// </param>
/// <param name="RequiresView">
/// <c>true</c> when at least one predicate reads a column that only the view has. While this is
/// <c>false</c> the same clause can be pointed at <c>appraisal.Appraisals</c> instead — see
/// <see cref="BaseTableWhereClause"/> — which is dramatically cheaper for counting.
/// </param>
internal sealed record AppraisalFilterSql(
    string WhereClause,
    DynamicParameters Parameters,
    bool RequiresView)
{
    /// <summary>
    /// True when a free-text search is in play, i.e. <see cref="SearchSource"/> is non-empty.
    /// Callers use it to pick the query hint: the union expands to a 17-way UNION of
    /// <c>LIKE @SearchPattern</c> arms, and from an unknown parameter the optimizer cannot tell the
    /// pattern is a prefix — so it plans for a possible leading wildcard and scans. Compiled per
    /// execution it sees the real value and seeks. Measured on 105k appraisals: count 219 -> 103 ms,
    /// paged 226 -> 149 ms. FORCE ORDER travels with it — see <see cref="ViewFrom"/>.
    ///
    /// Deliberately not a positional record parameter, like everything else below it: the three
    /// positional members are the ones a caller can reasonably read in isolation, and adding to
    /// them changes the generated Deconstruct out from under every call site.
    /// </summary>
    public bool HasFreeTextSearch { get; init; }

    /// <summary>
    /// The free-text search as a derived table — <c>(SELECT DISTINCT m.AppraisalId FROM (…) m)</c>
    /// — or empty when there is no search. Not part of <see cref="WhereClause"/>: it belongs at the
    /// FRONT of the FROM, which is what <see cref="ViewFrom"/> and <see cref="BaseTableFrom"/> are
    /// for.
    /// </summary>
    public string SearchSource { get; init; } = "";

    /// <summary>
    /// FROM fragment for the view, always aliased <c>v</c>, with the search joined in front of it
    /// when there is one.
    ///
    /// The order matters and is enforced with <c>OPTION (RECOMPILE, FORCE ORDER)</c> at the call
    /// site. Written as <c>Id IN (union)</c> instead, the optimizer would re-run the union once per
    /// view row whenever it thinks it can walk the view in sort order and stop early — which it
    /// does for the default <c>ORDER BY CreatedAt DESC</c>. On a leading-wildcard term that cost
    /// 10 s (711k scans of request.RequestTitles) against ~300 ms for this shape. HASH JOIN was
    /// tried instead and is not usable: it fails outright with Msg 8622 when the sort is
    /// CustomerName, because the view's OUTER APPLYs cannot all be hashed.
    ///
    /// The derived table exposes only <c>AppraisalId</c>, so every unqualified column in
    /// <see cref="WhereClause"/> and in BuildOrderBy's output still resolves to the view.
    /// </summary>
    public string ViewFrom =>
        SearchSource.Length == 0
            ? "appraisal.vw_AppraisalList v"
            : $"{SearchSource} s JOIN appraisal.vw_AppraisalList v ON v.Id = s.AppraisalId";

    /// <summary>
    /// The same shape aimed at <c>appraisal.Appraisals</c>, aliased <c>t</c>. Pair with
    /// <see cref="BaseTableWhereClause"/>, and only when <see cref="RequiresView"/> is false.
    /// </summary>
    public string BaseTableFrom =>
        SearchSource.Length == 0
            ? "appraisal.Appraisals t"
            : $"{SearchSource} s JOIN appraisal.Appraisals t ON t.Id = s.AppraisalId";

    /// <summary>
    /// The query hint every statement carrying <see cref="SearchSource"/> needs.
    ///
    /// ⚠ FORCE ORDER fixes the join order, so `search=` sent TOGETHER with a pinned field
    /// (customerName / appraisalNumber / requestNumber) is a shape to watch: the search ids are
    /// materialised and the view's OUTER APPLYs run for all of them before the far more selective
    /// pinned predicate can cut the set down, and the optimizer is not allowed to reorder that.
    /// The UI cannot produce it — AppraisalListPage sends the term to `search` OR to one pinned
    /// field, never both, and the export reuses that same object — but AppraisalListQueryParams
    /// accepts both, so an API caller or a future screen can. Left unguarded on purpose: there is
    /// no such caller today, and a guard written for an imagined one would be the wrong guard.
    ///
    /// A literal, never assembled from anything the caller supplies.
    /// </summary>
    public string SearchQueryHint => HasFreeTextSearch ? " OPTION (RECOMPILE, FORCE ORDER)" : "";

    /// <summary>
    /// The same clause aimed at <c>appraisal.Appraisals</c>. The view supplies
    /// <c>WHERE a.IsDeleted = 0</c> of its own; the base table does not, so it is added here.
    /// Only meaningful when <see cref="RequiresView"/> is <c>false</c>.
    /// </summary>
    public string BaseTableWhereClause =>
        WhereClause.Length == 0 ? " WHERE IsDeleted = 0" : WhereClause + " AND IsDeleted = 0";
}
