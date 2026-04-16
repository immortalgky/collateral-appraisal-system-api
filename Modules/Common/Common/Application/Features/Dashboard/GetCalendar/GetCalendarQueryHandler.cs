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

        // Parse YYYY-MM into a [firstOfMonth, firstOfNextMonth) range.
        if (!TryParseMonth(query.Month, out var firstOfMonth, out var firstOfNextMonth))
            throw new ArgumentException($"Invalid month format '{query.Month}'. Expected YYYY-MM.");

        // UNION three calendar event sources:
        //
        // Source 1: Meetings where I am a member (workflow.Meetings + workflow.MeetingMembers).
        //           Meetings are in statuses Draft or Scheduled (active). StartAt drives the date.
        //
        // Source 2: My task due dates from workflow.vw_TaskList filtered by AssigneeUserId
        //           and DueAt within the month.
        //
        // Source 3: SLA deadline — workflow.vw_SlaTaskList surfaces DueAt per task; we reuse
        //           it for tasks with SlaStatus = 'AtRisk' or 'Breached' assigned to me.
        //           This gives a "SLA deadline" entry on the day the task is due.
        //
        // Cap at 200 rows to prevent oversized payloads.
        const string sql = """
            SELECT TOP 200
                EventDate,
                Type,
                Title,
                EventTime,
                LinkEntityType,
                LinkEntityId
            FROM (
                -- Source 1: meetings I am a member of
                SELECT
                    CAST(m.StartAt AS date)                  AS EventDate,
                    'meeting'                                AS Type,
                    m.Title                                  AS Title,
                    CAST(m.StartAt AS time)                  AS EventTime,
                    'meeting'                                AS LinkEntityType,
                    m.Id                                     AS LinkEntityId
                FROM workflow.Meetings m
                INNER JOIN workflow.MeetingMembers mm
                    ON mm.MeetingId = m.Id
                   AND mm.UserId    = @Username
                WHERE m.Status IN ('Draft', 'Scheduled')
                  AND m.StartAt IS NOT NULL
                  AND m.StartAt >= @FirstOfMonth
                  AND m.StartAt <  @FirstOfNextMonth

                UNION ALL

                -- Source 2: my task due dates
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
                    COALESCE(tl.AppraisalId, tl.RequestId, tl.Id) AS LinkEntityId
                FROM workflow.vw_TaskList tl
                WHERE tl.AssigneeUserId  = @Username
                  AND tl.DueAt          IS NOT NULL
                  AND tl.DueAt          >= @FirstOfMonth
                  AND tl.DueAt          <  @FirstOfNextMonth

                UNION ALL

                -- Source 3: SLA deadlines for tasks assigned to me (AtRisk or Breached)
                SELECT
                    CAST(sl.DueAt AS date)                   AS EventDate,
                    'sla_deadline'                           AS Type,
                    CONCAT(
                        'SLA deadline: ',
                        COALESCE(
                            sl.TaskDescription,
                            CAST(sl.TaskName AS nvarchar(200))
                        )
                    )                                        AS Title,
                    CAST(sl.DueAt AS time)                   AS EventTime,
                    CASE
                        WHEN sl.AppraisalId IS NOT NULL THEN 'appraisal'
                        WHEN sl.RequestId   IS NOT NULL THEN 'request'
                        ELSE 'task'
                    END                                      AS LinkEntityType,
                    COALESCE(sl.AppraisalId, sl.RequestId, sl.TaskId) AS LinkEntityId
                FROM workflow.vw_SlaTaskList sl
                WHERE sl.AssignedTo     = @Username
                  AND sl.SlaStatus      IN ('AtRisk', 'Breached')
                  AND sl.DueAt         >= @FirstOfMonth
                  AND sl.DueAt         <  @FirstOfNextMonth
            ) src
            ORDER BY EventDate, EventTime
            """;

        var parameters = new DynamicParameters();
        parameters.Add("Username", username);
        parameters.Add("FirstOfMonth", firstOfMonth.ToDateTime(TimeOnly.MinValue));
        parameters.Add("FirstOfNextMonth", firstOfNextMonth.ToDateTime(TimeOnly.MinValue));

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
                    r.LinkEntityId))
                .ToList()))
            .ToList();

        return new GetCalendarResponse(days);
    }

    private static bool TryParseMonth(
        string? month,
        out DateOnly firstOfMonth,
        out DateOnly firstOfNextMonth)
    {
        firstOfMonth = default;
        firstOfNextMonth = default;

        if (string.IsNullOrWhiteSpace(month))
            return false;

        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out firstOfMonth))
            return false;

        firstOfNextMonth = firstOfMonth.AddMonths(1);
        return true;
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
    }
}
