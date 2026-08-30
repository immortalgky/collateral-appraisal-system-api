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
            // Text search across AppraisalNumber, CustomerName, and RequestNumber
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                conditions.Add(
                    "(AppraisalNumber LIKE '%' + @Search + '%' ESCAPE '\\' OR CustomerName LIKE '%' + @Search + '%' ESCAPE '\\' OR RequestNumber LIKE '%' + @Search + '%' ESCAPE '\\')");
                parameters.Add("Search", EscapeLikePattern(filter.Search.Trim()));
                // AppraisalNumber is on the base table, but CustomerName and RequestNumber are not,
                // and the three are OR'ed — so the whole predicate needs the view.
                requiresView = true;
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
                conditions.Add("CustomerName LIKE '%' + @CustomerName + '%' ESCAPE '\\'");
                parameters.Add("CustomerName", EscapeLikePattern(filter.CustomerName.Trim()));
                requiresView = true;
            }

            if (!string.IsNullOrWhiteSpace(filter.RequestNumber))
            {
                conditions.Add("RequestNumber LIKE '%' + @RequestNumber + '%' ESCAPE '\\'");
                parameters.Add("RequestNumber", EscapeLikePattern(filter.RequestNumber.Trim()));
                // RequestNumber comes from the LEFT JOIN on request.Requests, not the base table.
                requiresView = true;
            }

            if (!string.IsNullOrWhiteSpace(filter.AppraisalNumber))
            {
                conditions.Add("AppraisalNumber LIKE '%' + @AppraisalNumber + '%' ESCAPE '\\'");
                parameters.Add("AppraisalNumber", EscapeLikePattern(filter.AppraisalNumber.Trim()));
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
        return new AppraisalFilterSql(whereClause, parameters, requiresView);
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

    /// <summary>
    /// Makes the LIKE metacharacters <c>% _ [ \</c> literal, so someone searching for "50%" or
    /// "A_1" gets what they typed instead of a wildcard match. Every LIKE built here pairs this
    /// with an ESCAPE clause — the escaping does nothing without it.
    ///
    /// Same rule as TaskListFilterBuilder.EscapeLikePattern. We deliberately do NOT adopt that
    /// builder's prefix-by-default BuildSearchPattern: it is faster, but it silently changes what
    /// an existing search matches.
    /// </summary>
    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");

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
    /// The same clause aimed at <c>appraisal.Appraisals</c>. The view supplies
    /// <c>WHERE a.IsDeleted = 0</c> of its own; the base table does not, so it is added here.
    /// Only meaningful when <see cref="RequiresView"/> is <c>false</c>.
    /// </summary>
    public string BaseTableWhereClause =>
        WhereClause.Length == 0 ? " WHERE IsDeleted = 0" : WhereClause + " AND IsDeleted = 0";
}
