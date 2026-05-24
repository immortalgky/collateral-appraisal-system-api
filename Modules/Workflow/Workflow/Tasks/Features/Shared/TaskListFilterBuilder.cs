using Dapper;

namespace Workflow.Tasks.Features.Shared;

/// <summary>
/// Common filter/search surface shared by the task-list endpoints that query
/// <c>workflow.vw_TaskList</c> (currently <c>/tasks/me</c> and <c>/tasks/pool</c>).
/// Both filter requests implement this so their WHERE/ORDER BY building stays in
/// lockstep via <see cref="TaskListFilterBuilder"/>.
/// </summary>
public interface ITaskListFilter
{
    string? Status { get; }
    string? Priority { get; }
    string? TaskName { get; }
    string? Search { get; }
    string? AppraisalNumber { get; }
    string? CustomerName { get; }
    string? TaskStatus { get; }
    string? TaskType { get; }
    DateTime? DateFrom { get; }
    DateTime? DateTo { get; }
    DateTime? AppointmentDateFrom { get; }
    DateTime? AppointmentDateTo { get; }
    DateTime? RequestedAtFrom { get; }
    DateTime? RequestedAtTo { get; }
    string? SortBy { get; }
    string? SortDir { get; }
    string? SlaStatus { get; }
    string? ActivityId { get; }
}

/// <summary>
/// Translates an <see cref="ITaskListFilter"/> into parameterized WHERE clauses and a
/// validated ORDER BY for <c>workflow.vw_TaskList</c> queries. All values flow through
/// Dapper parameters; the only interpolated identifiers (sort field/direction) are
/// validated against <see cref="AllowedSortFields"/> and coerced to ASC/DESC.
/// </summary>
public static class TaskListFilterBuilder
{
    private const string DefaultSortField = "AssignedDate";

    /// <summary>
    /// Escapes SQL Server LIKE wildcards (<c>%</c>, <c>_</c>, <c>[</c>) so user-supplied search
    /// text is matched literally. Pair with an <c>ESCAPE '\'</c> clause. The escape character
    /// <c>\</c> itself is escaped first to avoid double-escaping.
    /// </summary>
    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");

    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "RequestNumber", "CustomerName", "TaskType", "Purpose", "PropertyType",
        "Status", "AppointmentDateTime", "RequestedBy", "RequestReceivedDate",
        "AssignedDate", "Movement", "InternalFollowupStaff", "Appraiser",
        "Priority", "DueAt", "SlaStatus", "ElapsedHours", "RemainingHours"
    };

    /// <summary>
    /// Appends WHERE conditions and their parameters for every populated filter field.
    /// Caller-specific conditions (e.g. AssignedType, access scoping) should already be
    /// in <paramref name="conditions"/>/<paramref name="parameters"/>.
    /// </summary>
    public static void ApplyFilters(
        ITaskListFilter? filter,
        List<string> conditions,
        DynamicParameters parameters)
    {
        if (filter is null)
            return;

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            conditions.Add("Status = @Status");
            parameters.Add("Status", filter.Status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Priority))
        {
            conditions.Add("Priority = @Priority");
            parameters.Add("Priority", filter.Priority);
        }

        if (!string.IsNullOrWhiteSpace(filter.TaskName))
        {
            conditions.Add("TaskType = @TaskName");
            parameters.Add("TaskName", filter.TaskName);
        }

        if (!string.IsNullOrWhiteSpace(filter.ActivityId))
        {
            conditions.Add("ActivityId = @ActivityId");
            parameters.Add("ActivityId", filter.ActivityId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add(
                "(AppraisalNumber LIKE '%' + @Search + '%' ESCAPE '\\' OR CustomerName LIKE '%' + @Search + '%' ESCAPE '\\')");
            parameters.Add("Search", EscapeLikePattern(filter.Search));
        }

        if (!string.IsNullOrWhiteSpace(filter.AppraisalNumber))
        {
            conditions.Add("AppraisalNumber LIKE '%' + @AppraisalNumber + '%' ESCAPE '\\'");
            parameters.Add("AppraisalNumber", EscapeLikePattern(filter.AppraisalNumber));
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
        {
            conditions.Add("CustomerName LIKE '%' + @CustomerName + '%' ESCAPE '\\'");
            parameters.Add("CustomerName", EscapeLikePattern(filter.CustomerName));
        }

        if (!string.IsNullOrWhiteSpace(filter.TaskStatus))
        {
            conditions.Add("PendingTaskStatus = @TaskStatus");
            parameters.Add("TaskStatus", filter.TaskStatus);
        }

        if (!string.IsNullOrWhiteSpace(filter.TaskType))
        {
            conditions.Add("TaskType = @TaskType");
            parameters.Add("TaskType", filter.TaskType);
        }

        if (filter.DateFrom.HasValue)
        {
            conditions.Add("AssignedDate >= @DateFrom");
            parameters.Add("DateFrom", filter.DateFrom.Value);
        }

        if (filter.DateTo.HasValue)
        {
            conditions.Add("AssignedDate < DATEADD(day, 1, @DateTo)");
            parameters.Add("DateTo", filter.DateTo.Value);
        }

        if (filter.AppointmentDateFrom.HasValue)
        {
            conditions.Add("AppointmentDateTime >= @AppointmentDateFrom");
            parameters.Add("AppointmentDateFrom", filter.AppointmentDateFrom.Value);
        }

        if (filter.AppointmentDateTo.HasValue)
        {
            conditions.Add("AppointmentDateTime < DATEADD(day, 1, @AppointmentDateTo)");
            parameters.Add("AppointmentDateTo", filter.AppointmentDateTo.Value);
        }

        if (filter.RequestedAtFrom.HasValue)
        {
            conditions.Add("RequestReceivedDate >= @RequestedAtFrom");
            parameters.Add("RequestedAtFrom", filter.RequestedAtFrom.Value);
        }

        if (filter.RequestedAtTo.HasValue)
        {
            conditions.Add("RequestReceivedDate < DATEADD(day, 1, @RequestedAtTo)");
            parameters.Add("RequestedAtTo", filter.RequestedAtTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SlaStatus))
        {
            conditions.Add("SlaStatus = @SlaStatus");
            parameters.Add("SlaStatus", filter.SlaStatus);
        }
    }

    /// <summary>
    /// Builds a safe ORDER BY expression. Falls back to <c>AssignedDate DESC</c> when the
    /// requested sort field is not allowlisted.
    /// </summary>
    public static string ResolveOrderBy(ITaskListFilter? filter)
    {
        var sortField = AllowedSortFields.Contains(filter?.SortBy ?? "") ? filter!.SortBy! : DefaultSortField;
        var sortDir = string.Equals(filter?.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        return $"{sortField} {sortDir}";
    }
}
