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
    /// Escapes SQL Server LIKE wildcards (<c>%</c>, <c>_</c>, <c>[</c>) so user-supplied search
    /// text is matched literally. Pair with an <c>ESCAPE '\'</c> clause. The escape character
    /// <c>\</c> itself is escaped first to avoid double-escaping.
    /// </summary>
    public static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");

    /// <summary>
    /// Builds a LIKE pattern with glob semantics for the task search box:
    /// <c>*</c> is the user wildcard (translated to <c>%</c>); all real LIKE metacharacters
    /// (<c>% _ [ \</c>) are escaped to literal via <see cref="EscapeLikePattern"/>. When the term
    /// contains no <c>*</c>, a trailing <c>%</c> is appended so the default is a seekable
    /// <b>prefix</b> search (<c>term%</c>) on <c>IX_RequestCustomer_Name</c> — fast and flat under
    /// load. Users opt into substring/suffix matching with <c>*</c> (e.g. <c>*somchai*</c>), which
    /// produces a leading wildcard and falls back to a scan. Pair with <c>ESCAPE '\'</c>.
    /// </summary>
    public static string BuildSearchPattern(string value)
    {
        var escaped = EscapeLikePattern(value);   // % _ [ \ -> literal; leaves * untouched
        var hasGlob = escaped.Contains('*');
        var pattern = escaped.Replace('*', '%');
        return hasGlob ? pattern : pattern + "%"; // no * => prefix search
    }

    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "RequestNumber", "CustomerName", "TaskType", "Purpose", "PropertyType",
        "Status", "AppointmentDateTime", "RequestedBy", "RequestReceivedDate",
        "AssignedDate", "Movement", "InternalFollowupStaff", "Appraiser",
        "Priority", "DueAt", "SlaStatus", "ElapsedHours", "RemainingHours"
    };

    /// <summary>
    /// A single WHERE condition plus whether it references columns that only exist
    /// after the <c>vw_TaskList</c> enrichment joins (<see cref="IsEnriched"/> = true),
    /// versus base <c>workflow.PendingTasks</c> columns (false). Base-only filter sets
    /// allow a cheap COUNT straight off <c>PendingTasks</c>; see <see cref="BaseCountSource"/>.
    /// </summary>
    public readonly record struct TaskFilterCondition(string Sql, bool IsEnriched);

    /// <summary>
    /// COUNT shell over a base-column projection of <c>workflow.PendingTasks</c>. The
    /// projected aliases match the <c>vw_TaskList</c> column names the base conditions
    /// and pool-access clause reference (<c>AssigneeUserId</c>, <c>PendingTaskStatus</c>,
    /// <c>TaskType</c>, <c>AssignedDate</c>, …) so the SAME condition strings/parameters
    /// can be reused verbatim. Append <c>" WHERE " + conditions</c>. Use ONLY when no
    /// enriched filter is active — otherwise the count must run against the full view.
    ///
    /// EXACTNESS INVARIANT: this counts straight off <c>PendingTasks</c>, while the
    /// <c>vw_TaskList</c> data query INNER-JOINs each task type to its owning table
    /// (Requests/QuotationRequests/DocumentFollowups/FeeAppointmentApprovals by
    /// CorrelationId). So the count equals the listed rows only while NO orphan
    /// PendingTask exists (CorrelationId matching none of the four — by construction
    /// always one of them; verified by docs/task-list/parity_vw_TaskList.sql's orphan
    /// probe). If orphan-tolerance is ever introduced, add a matching
    /// <c>WHERE EXISTS(...4 owning tables...)</c> guard here to keep count == data.
    /// </summary>
    public const string BaseCountSource =
        @"SELECT COUNT(*)
FROM (SELECT AssignedType,
             AssignedTo        AS AssigneeUserId,
             AssigneeCompanyId AS AssigneeCompanyId,
             ActivityId,
             TaskStatus        AS PendingTaskStatus,
             TaskName          AS TaskType,
             AssignedAt        AS AssignedDate,
             SlaStatus
      FROM workflow.PendingTasks) t";

    /// <summary>
    /// Builds the parameterized WHERE conditions for every populated filter field,
    /// tagging each as base or enriched, and registers their parameters on
    /// <paramref name="parameters"/>. Call ONCE per request (it mutates the parameter bag).
    /// </summary>
    public static List<TaskFilterCondition> BuildConditions(
        ITaskListFilter? filter,
        DynamicParameters parameters)
    {
        var conditions = new List<TaskFilterCondition>();
        if (filter is null)
            return conditions;

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            conditions.Add(new("Status = @Status", IsEnriched: true));
            parameters.Add("Status", filter.Status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Priority))
        {
            conditions.Add(new("Priority = @Priority", IsEnriched: true));
            parameters.Add("Priority", filter.Priority);
        }

        if (!string.IsNullOrWhiteSpace(filter.TaskName))
        {
            conditions.Add(new("TaskType = @TaskName", IsEnriched: false));
            parameters.Add("TaskName", filter.TaskName);
        }

        if (!string.IsNullOrWhiteSpace(filter.ActivityId))
        {
            conditions.Add(new("ActivityId = @ActivityId", IsEnriched: false));
            parameters.Add("ActivityId", filter.ActivityId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add(new(
                "(AppraisalNumber LIKE '%' + @Search + '%' ESCAPE '\\' OR CustomerName LIKE '%' + @Search + '%' ESCAPE '\\')",
                IsEnriched: true));
            parameters.Add("Search", EscapeLikePattern(filter.Search));
        }

        if (!string.IsNullOrWhiteSpace(filter.AppraisalNumber))
        {
            conditions.Add(new("AppraisalNumber LIKE '%' + @AppraisalNumber + '%' ESCAPE '\\'", IsEnriched: true));
            parameters.Add("AppraisalNumber", EscapeLikePattern(filter.AppraisalNumber));
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
        {
            conditions.Add(new("CustomerName LIKE '%' + @CustomerName + '%' ESCAPE '\\'", IsEnriched: true));
            parameters.Add("CustomerName", EscapeLikePattern(filter.CustomerName));
        }

        if (!string.IsNullOrWhiteSpace(filter.TaskStatus))
        {
            conditions.Add(new("PendingTaskStatus = @TaskStatus", IsEnriched: false));
            parameters.Add("TaskStatus", filter.TaskStatus);
        }

        if (!string.IsNullOrWhiteSpace(filter.TaskType))
        {
            conditions.Add(new("TaskType = @TaskType", IsEnriched: false));
            parameters.Add("TaskType", filter.TaskType);
        }

        if (filter.DateFrom.HasValue)
        {
            conditions.Add(new("AssignedDate >= @DateFrom", IsEnriched: false));
            parameters.Add("DateFrom", filter.DateFrom.Value);
        }

        if (filter.DateTo.HasValue)
        {
            conditions.Add(new("AssignedDate < DATEADD(day, 1, @DateTo)", IsEnriched: false));
            parameters.Add("DateTo", filter.DateTo.Value);
        }

        if (filter.AppointmentDateFrom.HasValue)
        {
            conditions.Add(new("AppointmentDateTime >= @AppointmentDateFrom", IsEnriched: true));
            parameters.Add("AppointmentDateFrom", filter.AppointmentDateFrom.Value);
        }

        if (filter.AppointmentDateTo.HasValue)
        {
            conditions.Add(new("AppointmentDateTime < DATEADD(day, 1, @AppointmentDateTo)", IsEnriched: true));
            parameters.Add("AppointmentDateTo", filter.AppointmentDateTo.Value);
        }

        if (filter.RequestedAtFrom.HasValue)
        {
            conditions.Add(new("RequestReceivedDate >= @RequestedAtFrom", IsEnriched: true));
            parameters.Add("RequestedAtFrom", filter.RequestedAtFrom.Value);
        }

        if (filter.RequestedAtTo.HasValue)
        {
            conditions.Add(new("RequestReceivedDate < DATEADD(day, 1, @RequestedAtTo)", IsEnriched: true));
            parameters.Add("RequestedAtTo", filter.RequestedAtTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SlaStatus))
        {
            conditions.Add(new("SlaStatus = @SlaStatus", IsEnriched: false));
            parameters.Add("SlaStatus", filter.SlaStatus);
        }

        return conditions;
    }

    /// <summary>
    /// Builds a safe ORDER BY expression. Falls back to <c>AssignedDate DESC</c> when the
    /// requested sort field is not allowlisted.
    /// </summary>
    public static string ResolveOrderBy(ITaskListFilter? filter)
    {
        var sortField = AllowedSortFields.Contains(filter?.SortBy ?? "") ? filter!.SortBy! : DefaultSortField;
        var sortDir = string.Equals(filter?.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        // ElapsedHours/RemainingHours are no longer columns on vw_TaskList (computed in C# via
        // IBusinessTimeCalculator). Their business-time values are monotonic in the underlying
        // timestamps, so translate the sort for exact ordering — and to avoid ORDER BY referencing
        // a dropped column:
        //   ElapsedHours  ASC  ≡ AssignedDate DESC (least elapsed = most recent assignment)
        //   RemainingHours ASC ≡ DueAt        ASC  (least remaining = earliest due)
        return sortField switch
        {
            "ElapsedHours" => $"AssignedDate {Invert(sortDir)}",
            "RemainingHours" => $"DueAt {sortDir}",
            _ => $"{sortField} {sortDir}"
        };
    }

    private static string Invert(string dir) =>
        string.Equals(dir, "ASC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
}
