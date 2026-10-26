using Dapper;
using Shared.Data;

namespace Workflow.Tasks.Features.Shared;

/// <summary>
/// Common filter/search surface of the task-list endpoints (<c>/tasks/me</c>, <c>/tasks/pool</c>
/// and <c>/tasks</c>). <c>/tasks/me</c> and <c>/tasks/pool</c> hand it to the stored procs through
/// <see cref="TaskListProcParams"/>; only <c>/tasks</c> queries <c>workflow.vw_TaskList</c> directly and
/// builds its WHERE/ORDER BY here via <see cref="TaskListFilterBuilder.BuildConditions"/> and
/// <see cref="TaskListFilterBuilder.ResolveOrderBy"/>.
/// </summary>
public interface ITaskListFilter
{
    string? Status { get; }
    string? Priority { get; }
    string? TaskName { get; }
    string? Search { get; }
    string? AppraisalNumber { get; }
    string? Channel { get; }
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
    string? Purpose { get; }
    string? TaskStatusBucket { get; }
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
    /// Builds a LIKE pattern with glob semantics for the task search box:
    /// <c>*</c> is the user wildcard (translated to <c>%</c>); all real LIKE metacharacters
    /// (<c>% _ [ \</c>) are escaped to literal via <see cref="LikePattern.Escape"/>. When the term
    /// contains no <c>*</c>, a trailing <c>%</c> is appended so the default is a seekable
    /// <b>prefix</b> search (<c>term%</c>) on <c>IX_RequestCustomer_Name</c> — fast and flat under
    /// load. Users opt into substring/suffix matching with <c>*</c> (e.g. <c>*somchai*</c>), which
    /// produces a leading wildcard and falls back to a scan. Pair with <c>ESCAPE '\'</c>.
    /// </summary>
    public static string BuildSearchPattern(string value) => LikePattern.Build(value);

    private const int MaxListItemLength = 200;

    /// <summary>
    /// Normalises a multi-value filter (comma-separated, e.g. <c>Breached,NoSla</c>) for the stored
    /// procs, which split it with <c>STRING_SPLIT</c>: items are trimmed, empty ones dropped, and a
    /// value with no items left collapses to NULL (= filter off). A single value passes through.
    /// Items longer than <see cref="MaxListItemLength"/> are cut to it: no valid code is that long, so
    /// the cut item simply matches nothing (invalid input never widens results), and it can no longer
    /// overflow the procs' table variables (Msg 2628 -> 500).
    /// </summary>
    public static string? NormalizeList(string? value)
    {
        var items = value?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.Length > MaxListItemLength ? v[..MaxListItemLength] : v)
            .ToArray();
        return items is { Length: > 0 } ? string.Join(",", items) : null;
    }

    /// <summary>
    /// <see cref="NormalizeList"/> for SlaStatus, plus the vocabulary bridge: task rows can be stored as
    /// <c>OnTrack</c> while the board lane (and the UI) calls it <c>OnTime</c>, so a list containing
    /// <c>OnTime</c> also gets <c>OnTrack</c> (deduped, case-insensitive). <c>NoSla</c> passes through.
    /// </summary>
    public static string? NormalizeSlaStatus(string? value)
    {
        var list = NormalizeList(value);
        if (list is null) return null;

        var items = list.Split(',').Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (items.Contains("OnTime", StringComparer.OrdinalIgnoreCase)
            && !items.Contains("OnTrack", StringComparer.OrdinalIgnoreCase))
            items.Add("OnTrack");
        return string.Join(",", items);
    }

    /// <summary>
    /// Canonical casing of a lifecycle bucket (<c>notstarted</c> -> <c>NotStarted</c>), trimmed; unknown
    /// values are kept as-is (the procs and <see cref="BuildConditions"/> then match nothing); blank = NULL.
    /// </summary>
    public static string? NormalizeBucket(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var v = value.Trim();
        return v.ToLowerInvariant() switch
        {
            "notstarted" => "NotStarted",
            "inprogress" => "InProgress",
            "overdue" => "Overdue",
            _ => v
        };
    }

    // Sparse columns (no SLA / no submitted report): rows without a value must not float to the top.
    private static readonly HashSet<string> NullsLastColumns = new() { "DueAt", "ReportReceivedAt" };

    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "RequestNumber", "CustomerName", "TaskType", "Purpose", "PropertyType",
        "Status", "AppointmentDateTime", "RequestedBy", "RequestReceivedDate",
        "AssignedDate", "Movement", "InternalFollowupStaff", "Appraiser",
        "Priority", "DueAt", "SlaStatus", "ElapsedHours", "RemainingHours",
        "Channel", "RequestedAt", "ReportReceivedAt"
    };

    /// <summary>
    /// Builds the parameterized WHERE conditions for the direct <c>workflow.vw_TaskList</c> query
    /// (<c>GET /tasks</c>) from every populated filter field, mirroring the filter surface of
    /// <c>workflow.sp_GetTaskList</c>, and registers their parameters on <paramref name="parameters"/>.
    /// Multi-value fields (<see cref="NormalizeList"/>) are bound as ONE csv parameter and split in SQL
    /// with <c>STRING_SPLIT</c>, so the parameter count does not grow with the list. Call ONCE per
    /// request (it mutates the parameter bag).
    /// </summary>
    public static List<string> BuildConditions(
        ITaskListFilter? filter,
        DynamicParameters parameters)
    {
        var conditions = new List<string>();
        if (filter is null)
            return conditions;

        AddList(conditions, parameters, filter.Status, "Status", "Status IN (SELECT value FROM STRING_SPLIT(@Status, ','))");

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

        AddList(conditions, parameters, filter.ActivityId, "ActivityId",
            "ActivityId IN (SELECT value FROM STRING_SPLIT(@ActivityId, ','))");
        AddList(conditions, parameters, filter.Channel, "Channel",
            "Channel IN (SELECT value FROM STRING_SPLIT(@Channel, ','))");
        AddList(conditions, parameters, filter.Purpose, "Purpose",
            "Purpose IN (SELECT value FROM STRING_SPLIT(@Purpose, ','))");

        // Same glob semantics as the proc: prefix by default, user '*' = wildcard (see BuildSearchPattern).
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add(
                "(AppraisalNumber LIKE @Search ESCAPE '\\' OR CustomerName LIKE @Search ESCAPE '\\')");
            parameters.Add("Search", BuildSearchPattern(filter.Search));
        }

        if (!string.IsNullOrWhiteSpace(filter.AppraisalNumber))
        {
            conditions.Add("AppraisalNumber LIKE @AppraisalNumber ESCAPE '\\'");
            parameters.Add("AppraisalNumber", BuildSearchPattern(filter.AppraisalNumber));
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
        {
            conditions.Add("CustomerName LIKE @CustomerName ESCAPE '\\'");
            parameters.Add("CustomerName", BuildSearchPattern(filter.CustomerName));
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

        // 'NoSla' is the sentinel for SlaStatus IS NULL and may be mixed with real statuses.
        var sla = (NormalizeSlaStatus(filter.SlaStatus) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        var noSla = sla.Any(v => string.Equals(v, "NoSla", StringComparison.OrdinalIgnoreCase));
        var slaStatuses = string.Join(",", sla.Where(v => !string.Equals(v, "NoSla", StringComparison.OrdinalIgnoreCase)));
        if (slaStatuses.Length > 0)
        {
            conditions.Add(noSla
                ? "(SlaStatus IN (SELECT value FROM STRING_SPLIT(@SlaStatus, ',')) OR SlaStatus IS NULL)"
                : "SlaStatus IN (SELECT value FROM STRING_SPLIT(@SlaStatus, ','))");
            parameters.Add("SlaStatus", slaStatuses);
        }
        else if (noSla)
        {
            conditions.Add("SlaStatus IS NULL");
        }

        // Mutually exclusive lifecycle buckets; an unknown value matches nothing (as in the proc).
        var bucket = NormalizeBucket(filter.TaskStatusBucket);
        if (bucket is not null)
        {
            conditions.Add(bucket switch
            {
                "NotStarted" => "(PendingTaskStatus = 'Assigned' AND (SlaStatus IS NULL OR SlaStatus <> 'Breached'))",
                "InProgress" =>
                    "(PendingTaskStatus IN ('InProgress','Completing') AND (SlaStatus IS NULL OR SlaStatus <> 'Breached'))",
                "Overdue" => "SlaStatus = 'Breached'",
                _ => "1 = 0"
            });
        }

        return conditions;
    }

    private static void AddList(
        List<string> conditions, DynamicParameters parameters, string? value, string name, string condition)
    {
        var list = NormalizeList(value);
        if (list is null) return;

        conditions.Add(condition);
        parameters.Add(name, list);
    }

    /// <summary>
    /// Builds a safe ORDER BY expression. Falls back to <c>AssignedDate DESC</c> when the
    /// requested sort field is not allowlisted.
    /// </summary>
    public static string ResolveOrderBy(ITaskListFilter? filter)
    {
        // Emit the allowlist's canonical spelling, not the caller's (matching is case-insensitive, so
        // the FE's camelCase keys such as 'elapsedHours' resolve to a real column).
        var sortField = AllowedSortFields.TryGetValue(filter?.SortBy ?? "", out var canonical)
            ? canonical
            : DefaultSortField;
        var sortDir = string.Equals(filter?.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        // ElapsedHours/RemainingHours are no longer columns on vw_TaskList (computed in C# via
        // IBusinessTimeCalculator). Their business-time values are monotonic in the underlying
        // timestamps, so translate the sort for exact ordering — and to avoid ORDER BY referencing
        // a dropped column:
        //   ElapsedHours  ASC  ≡ AssignedDate DESC (least elapsed = most recent assignment)
        //   RemainingHours ASC ≡ DueAt        ASC  (least remaining = earliest due)
        var (column, dir) = sortField switch
        {
            "ElapsedHours" => ("AssignedDate", Invert(sortDir)),
            "RemainingHours" => ("DueAt", sortDir),
            _ => (sortField, sortDir)
        };

        // Same tie-break as sp_GetTaskList (AssignedAt DESC, Id DESC; Id is the view's unique key) so
        // OFFSET/FETCH paging is stable on low-cardinality sorts and ties order like /tasks/me.
        // A column may appear only once in ORDER BY, hence no second AssignedDate.
        var tieBreak = column == "AssignedDate" ? "Id DESC" : "AssignedDate DESC, Id DESC";

        // NULLs sort FIRST under ASC in SQL Server; keep them last in both directions.
        var nullsLast = NullsLastColumns.Contains(column) ? $"CASE WHEN {column} IS NULL THEN 1 ELSE 0 END, " : "";
        return $"{nullsLast}{column} {dir}, {tieBreak}";
    }

    private static string Invert(string dir) =>
        string.Equals(dir, "ASC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
}
