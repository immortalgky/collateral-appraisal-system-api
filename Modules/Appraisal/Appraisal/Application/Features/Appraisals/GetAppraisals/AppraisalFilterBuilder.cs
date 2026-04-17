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
        "AppointmentDateTime", "Province", "Channel", "BankingSegment",
        "FacilityLimit", "PropertyCount", "ElapsedHours", "RemainingHours",
        "AssignmentType", "CompanyName", "RequestedAt"
    };

    public static (string WhereClause, DynamicParameters Parameters) BuildFilter(GetAppraisalsFilterRequest? filter)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

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

            // Exact match filters
            if (!string.IsNullOrWhiteSpace(filter.AssigneeUserId))
            {
                conditions.Add("AssigneeUserId = @AssigneeUserId");
                parameters.Add("AssigneeUserId", filter.AssigneeUserId);
            }

            if (!string.IsNullOrWhiteSpace(filter.AssigneeCompanyId))
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
        }

        var whereClause = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
        return (whereClause, parameters);
    }

    public static string BuildOrderBy(GetAppraisalsFilterRequest? filter)
    {
        var sortField = AllowedSortFields.Contains(filter?.SortBy ?? "") ? filter!.SortBy! : "CreatedAt";
        var sortDir = string.Equals(filter?.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        return $"{sortField} {sortDir}";
    }

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
