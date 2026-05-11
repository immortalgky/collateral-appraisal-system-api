using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;

namespace Common.Application.Features.Dashboard.GetCalendar;

public class GetCalendarQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService
) : IQueryHandler<GetCalendarQuery, GetCalendarResponse>
{
    public async Task<GetCalendarResponse> Handle(
        GetCalendarQuery query,
        CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new GetCalendarResponse(new List<CalendarDayDto>());

        // Parse the optional Type CSV into a set for O(1) lookup.
        // null/empty means all sources are included.
        var types = ParseTypes(query.Type);
        var includeMeeting = types is null || types.Contains("meeting");
        var includeTaskDue = types is null || types.Contains("task_due");

        // Build a UNION of only the requested event sources.
        // Skipping branches that aren't requested avoids scanning their tables,
        // which is especially helpful for Day/Week views with narrow ranges.
        var branches = new List<string>();

        if (includeMeeting)
            branches.Add("""
                -- Source 1: meetings I am a member of (not appraisal-scoped)
                SELECT
                    CAST(m.StartAt AS date)                  AS EventDate,
                    'meeting'                                AS Type,
                    m.Title                                  AS Title,
                    CAST(m.StartAt AS time)                  AS EventTime,
                    'meeting'                                AS LinkEntityType,
                    m.Id                                     AS LinkEntityId,
                    CAST(NULL AS nvarchar(50))               AS AppraisalNumber,
                    CAST(0 AS bit)                           AS IsSlaCritical
                FROM workflow.Meetings m
                INNER JOIN workflow.MeetingMembers mm
                    ON mm.MeetingId = m.Id
                   AND mm.UserId    = @Username
                WHERE m.Status IN ('Draft', 'Scheduled')
                  AND m.StartAt IS NOT NULL
                  AND m.StartAt >= @From
                  AND m.StartAt <  DATEADD(day, 1, @To)
                """);

        if (includeTaskDue)
            branches.Add("""
                -- Source 2: my task due dates. IsSlaCritical flags tasks whose SLA
                -- is AtRisk or Breached so the UI can color the dot red instead of
                -- yellow. No separate SLA branch — one row per task, urgency on top.
                SELECT
                    CAST(tl.DueAt AS date)                   AS EventDate,
                    'task_due'                               AS Type,
                    COALESCE(
                        tl.TaskDescription,
                        CAST(tl.TaskType AS nvarchar(200))
                    )                                        AS Title,
                    CAST(tl.DueAt AS time)                   AS EventTime,
                    CASE
                        WHEN tl.AppraisalId IS NOT NULL THEN 'appraisal'
                        WHEN tl.RequestId   IS NOT NULL THEN 'request'
                        ELSE 'task'
                    END                                      AS LinkEntityType,
                    COALESCE(tl.AppraisalId, tl.RequestId, tl.Id) AS LinkEntityId,
                    tl.AppraisalNumber                       AS AppraisalNumber,
                    CAST(
                        CASE WHEN EXISTS (
                            SELECT 1
                            FROM workflow.vw_SlaTaskList sla
                            WHERE sla.TaskId      = tl.Id
                              AND sla.AssignedTo  = @Username
                              AND sla.SlaStatus   IN ('AtRisk', 'Breached')
                        ) THEN 1 ELSE 0 END
                        AS bit
                    )                                        AS IsSlaCritical
                FROM workflow.vw_TaskList tl
                WHERE tl.AssigneeUserId  = @Username
                  AND tl.DueAt          IS NOT NULL
                  AND tl.DueAt          >= @From
                  AND tl.DueAt          <  DATEADD(day, 1, @To)
                """);

        // If somehow no branch is included (should not happen after endpoint validation),
        // return an empty response rather than building invalid SQL.
        if (branches.Count == 0)
            return new GetCalendarResponse(new List<CalendarDayDto>());

        var unionSql = string.Join("\n\nUNION ALL\n\n", branches);
        var sql = $"""
            SELECT TOP 200
                EventDate,
                Type,
                Title,
                EventTime,
                LinkEntityType,
                LinkEntityId,
                AppraisalNumber,
                IsSlaCritical
            FROM (
            {unionSql}
            ) src
            ORDER BY EventDate, EventTime
            """;

        var parameters = new DynamicParameters();
        parameters.Add("Username", username);
        parameters.Add("From", query.From.ToDateTime(TimeOnly.MinValue));
        parameters.Add("To", query.To.ToDateTime(TimeOnly.MinValue));

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<CalendarRow>(sql, parameters);

        // Group flat rows by date in C#. Convert DateTime → DateOnly and TimeSpan → TimeOnly
        // since Dapper materializes SQL date/time columns as DateTime/TimeSpan.
        var days = rows
            .GroupBy(r => DateOnly.FromDateTime(r.EventDate))
            .OrderBy(g => g.Key)
            .Select(g => new CalendarDayDto(
                g.Key,
                g.Select(r => new CalendarItemDto(
                    r.Type,
                    r.Title,
                    r.EventTime.HasValue ? TimeOnly.FromTimeSpan(r.EventTime.Value) : null,
                    r.LinkEntityType,
                    r.LinkEntityId,
                    r.AppraisalNumber,
                    r.IsSlaCritical))
                .ToList()))
            .ToList();

        return new GetCalendarResponse(days);
    }

    /// <summary>
    /// Parses a comma-separated type string into a lowercase set.
    /// Returns null when the input is null or empty (meaning "all types").
    /// </summary>
    private static HashSet<string>? ParseTypes(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return null;

        return type
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .ToHashSet();
    }

    // Private projection for Dapper mapping.
    // Dapper maps SQL `date` to DateTime and SQL `time` to TimeSpan;
    // the handler converts to DateOnly/TimeOnly when projecting to the DTO.
    private sealed class CalendarRow
    {
        public DateTime EventDate { get; init; }
        public string Type { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public TimeSpan? EventTime { get; init; }
        public string LinkEntityType { get; init; } = string.Empty;
        public Guid LinkEntityId { get; init; }
        public string? AppraisalNumber { get; init; }
        public bool IsSlaCritical { get; init; }
    }
}
