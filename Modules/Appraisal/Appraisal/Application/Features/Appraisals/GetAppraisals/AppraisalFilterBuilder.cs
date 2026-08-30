using Appraisal.Application.Features.Appraisals.Shared;
using Dapper;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

/// <summary>
/// Shared filter and sort builder for Appraisal list queries.
/// Used by both the paginated list handler and the export handler.
/// </summary>
internal static class AppraisalFilterBuilder
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "RequestNumber", "CustomerName", "Status", "AppraisalType",
        "Priority", "SLADueDate", "SLAStatus", "CreatedAt", "AssignedDate",
        "AppointmentDateTime", "Province", "District", "SubDistrict", "Channel", "BankingSegment",
        "FacilityLimit", "PropertyCount", "ElapsedHours", "RemainingHours",
        "AssignmentType", "CompanyName", "RequestedAt", "Purpose"
    };

    /// <param name="excludeStatus">
    /// Leaves the Status predicate out. Used by the status facet: counting the statuses through a
    /// WHERE that already pins one status returns a single chip, so the user cannot switch away
    /// from it. Every other filter still narrows the counts.
    /// </param>
    public static AppraisalFilterSql BuildFilter(
        GetAppraisalsFilterRequest? filter,
        Guid? enforcedCompanyId = null,
        bool excludeStatus = false)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // Set whenever a predicate reads a column that appraisal.Appraisals does not have — i.e.
        // one the view synthesises (latest assignment, first land location, customer, appointment).
        // While this stays false the caller may count and page straight off the base table.
        var requiresView = false;

        // See AppraisalFilterSql.HasFreeTextSearch for why this is tracked.
        var hasFreeTextSearch = false;

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
            // The replacement is a semi-join over base tables only, so the predicate is resolved
            // before the view does any work and requiresView stays false: the count runs off
            // appraisal.Appraisals. Measured on 105k appraisals: 738 ms -> 39 ms for the count.
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = AppraisalSearchPredicate.BuildIdFilter(filter.Search);
                if (search is null)
                {
                    // Shorter than the minimum useful term. Match nothing rather than everything —
                    // silently ignoring the box would show an unfiltered list that looks filtered.
                    conditions.Add("1 = 0");
                }
                else
                {
                    conditions.Add(search.Value.Sql);
                    parameters.AddDynamicParams(search.Value.Parameters);
                    hasFreeTextSearch = true;
                }
            }

            // Multi-value filters (comma-separated -> IN clause)
            if (!excludeStatus)
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

            if (!enforcedCompanyId.HasValue && !string.IsNullOrWhiteSpace(filter.AssigneeCompanyId))
            {
                conditions.Add("AssigneeCompanyId = @AssigneeCompanyId");
                parameters.Add("AssigneeCompanyId", filter.AssigneeCompanyId);
                requiresView = true;
            }

            if (!string.IsNullOrWhiteSpace(filter.Channel))
            {
                conditions.Add("Channel = @Channel");
                parameters.Add("Channel", filter.Channel);
            }

            if (!string.IsNullOrWhiteSpace(filter.BankingSegment))
            {
                conditions.Add("BankingSegment = @BankingSegment");
                parameters.Add("BankingSegment", filter.BankingSegment);
            }

            if (filter.IsPma.HasValue)
            {
                conditions.Add("IsPma = @IsPma");
                parameters.Add("IsPma", filter.IsPma.Value);
            }

            // Geographic filters
            if (!string.IsNullOrWhiteSpace(filter.Province))
            {
                conditions.Add("Province = @Province");
                parameters.Add("Province", filter.Province);
                requiresView = true;
            }

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
            if (!string.IsNullOrWhiteSpace(filter.CustomerName))
            {
                conditions.Add("CustomerName LIKE '%' + @CustomerName + '%'");
                parameters.Add("CustomerName", filter.CustomerName.Trim());
                requiresView = true;
            }

            if (!string.IsNullOrWhiteSpace(filter.AppraisalNumber))
            {
                conditions.Add("AppraisalNumber LIKE '%' + @AppraisalNumber + '%'");
                parameters.Add("AppraisalNumber", filter.AppraisalNumber.Trim());
            }

            if (!string.IsNullOrWhiteSpace(filter.SubDistrict))
            {
                conditions.Add("SubDistrict LIKE '%' + @SubDistrict + '%'");
                parameters.Add("SubDistrict", filter.SubDistrict.Trim());
                requiresView = true;
            }

            AddDateRangeFilter(conditions, parameters, filter.RequestedAtFrom, filter.RequestedAtTo,
                "RequestedAt", "RequestedAtFrom", "RequestedAtTo");
        }

        var whereClause = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
        return new AppraisalFilterSql(whereClause, parameters, requiresView)
        {
            HasFreeTextSearch = hasFreeTextSearch
        };
    }

    public static string BuildOrderBy(GetAppraisalsFilterRequest? filter)
    {
        var sortField = AllowedSortFields.Contains(filter?.SortBy ?? "") ? filter!.SortBy! : "CreatedAt";
        var sortDir = string.Equals(filter?.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        // ElapsedHours/RemainingHours are no longer columns on the view (computed in C# via
        // IBusinessTimeCalculator). Their business-time values are monotonic in the underlying
        // timestamps, so translate the sort for exact ordering:
        //   ElapsedHours  ASC  ≡ CreatedAt  DESC (least elapsed = most recently created)
        //   RemainingHours ASC ≡ SLADueDate ASC  (least remaining = earliest due)
        return sortField switch
        {
            "ElapsedHours" => $"CreatedAt {Invert(sortDir)}",
            "RemainingHours" => $"SLADueDate {sortDir}",
            _ => $"{sortField} {sortDir}"
        };
    }

    private static string Invert(string dir) =>
        string.Equals(dir, "ASC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

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
/// <see cref="BaseTableWhereClause"/> — which is dramatically cheaper for counting and faceting.
/// </param>
internal sealed record AppraisalFilterSql(
    string WhereClause,
    DynamicParameters Parameters,
    bool RequiresView)
{
    /// <summary>
    /// True when the free-text predicate is in the clause. It expands to a 17-way UNION of
    /// <c>LIKE @SearchPattern</c> arms, and from an unknown parameter the optimizer cannot tell the
    /// pattern is a prefix — so it plans for a possible leading wildcard and scans. Compiled per
    /// execution it sees the real value and seeks. Measured on 105k appraisals: count 219 -> 103 ms,
    /// paged 226 -> 149 ms, facet 228 -> 109 ms.
    ///
    /// Deliberately not a positional record parameter: the generated Deconstruct is 3-arity and
    /// ExportAppraisalsQueryHandler destructures it.
    /// </summary>
    public bool HasFreeTextSearch { get; init; }

    /// <summary>
    /// The same clause aimed at <c>appraisal.Appraisals</c>. The view supplies
    /// <c>WHERE a.IsDeleted = 0</c> of its own; the base table does not, so it is added here.
    /// Only meaningful when <see cref="RequiresView"/> is <c>false</c>.
    /// </summary>
    public string BaseTableWhereClause =>
        WhereClause.Length == 0 ? " WHERE IsDeleted = 0" : WhereClause + " AND IsDeleted = 0";
}
