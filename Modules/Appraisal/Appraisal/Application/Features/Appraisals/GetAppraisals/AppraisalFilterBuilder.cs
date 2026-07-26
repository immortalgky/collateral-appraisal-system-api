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

    public static (string WhereClause, DynamicParameters Parameters) BuildFilter(
        GetAppraisalsFilterRequest? filter,
        Guid? enforcedCompanyId = null)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // External (company) callers are always scoped to their own company; the caller-supplied
        // AssigneeCompanyId on the filter is ignored to prevent cross-company peeking.
        // AppraisalAssignments.AssigneeCompanyId is nvarchar(100), so bind a string — passing a
        // Guid forces SQL to TRY_CAST every column value to uniqueidentifier, which throws on
        // rows that hold non-GUID text.
        if (enforcedCompanyId.HasValue)
        {
            conditions.Add("AssigneeCompanyId = @ScopedCompanyId");
            parameters.Add("ScopedCompanyId", enforcedCompanyId.Value.ToString());
        }

        if (filter is not null)
        {
            // Text search across AppraisalNumber, CustomerName, and RequestNumber
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                conditions.Add(
                    "(AppraisalNumber LIKE '%' + @Search + '%' OR CustomerName LIKE '%' + @Search + '%' OR RequestNumber LIKE '%' + @Search + '%')");
                parameters.Add("Search", filter.Search.Trim());
            }

            // Multi-value filters (comma-separated -> IN clause)
            AddMultiValueFilter(conditions, parameters, filter.Status, "Status", "@Statuses");
            AddMultiValueFilter(conditions, parameters, filter.Priority, "Priority", "@Priorities");
            AddMultiValueFilter(conditions, parameters, filter.AppraisalType, "AppraisalType", "@AppraisalTypes");
            AddMultiValueFilter(conditions, parameters, filter.SlaStatus, "SLAStatus", "@SlaStatuses");
            AddMultiValueFilter(conditions, parameters, filter.AssignmentType, "AssignmentType", "@AssignmentTypes");
            AddMultiValueFilter(conditions, parameters, filter.Purpose, "Purpose", "@Purposes");
            AddPropertyTypeFilter(conditions, parameters, filter.PropertyType);

            // Exact match filters
            if (!string.IsNullOrWhiteSpace(filter.AssigneeUserId))
            {
                conditions.Add("AssigneeUserId = @AssigneeUserId");
                parameters.Add("AssigneeUserId", filter.AssigneeUserId);
            }

            if (!enforcedCompanyId.HasValue && !string.IsNullOrWhiteSpace(filter.AssigneeCompanyId))
            {
                conditions.Add("AssigneeCompanyId = @AssigneeCompanyId");
                parameters.Add("AssigneeCompanyId", filter.AssigneeCompanyId);
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
            }

            if (!string.IsNullOrWhiteSpace(filter.District))
            {
                conditions.Add("District = @District");
                parameters.Add("District", filter.District);
            }

            // Date range filters
            AddDateRangeFilter(conditions, parameters, filter.CreatedFrom, filter.CreatedTo,
                "CreatedAt", "CreatedFrom", "CreatedTo");

            AddDateRangeFilter(conditions, parameters, filter.SlaDueDateFrom, filter.SlaDueDateTo,
                "SLADueDate", "SlaDueDateFrom", "SlaDueDateTo");

            AddDateRangeFilter(conditions, parameters, filter.AssignedDateFrom, filter.AssignedDateTo,
                "AssignedDate", "AssignedDateFrom", "AssignedDateTo");

            AddDateRangeFilter(conditions, parameters, filter.AppointmentDateFrom, filter.AppointmentDateTo,
                "AppointmentDateTime", "AppointmentDateFrom", "AppointmentDateTo");

            // Picker-specific additive fields
            if (!string.IsNullOrWhiteSpace(filter.CustomerName))
            {
                conditions.Add("CustomerName LIKE '%' + @CustomerName + '%'");
                parameters.Add("CustomerName", filter.CustomerName.Trim());
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
            }

            AddDateRangeFilter(conditions, parameters, filter.RequestedAtFrom, filter.RequestedAtTo,
                "RequestedAt", "RequestedAtFrom", "RequestedAtTo");
        }

        var whereClause = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
        return (whereClause, parameters);
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

    private static void AddMultiValueFilter(
        List<string> conditions, DynamicParameters parameters,
        string? value, string columnName, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0) return;

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

    private static void AddDateRangeFilter(
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
    }
}
