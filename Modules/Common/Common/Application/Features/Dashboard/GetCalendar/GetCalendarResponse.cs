namespace Common.Application.Features.Dashboard.GetCalendar;

public sealed record GetCalendarResponse(List<CalendarDayDto> Items);

public sealed record CalendarDayDto(
    DateOnly Date,
    List<CalendarItemDto> Items);

public sealed record CalendarItemDto(
    string Type,           // "meeting" | "task_due" | "sla_deadline"
    string Title,
    TimeOnly? Time,
    string LinkEntityType,
    Guid LinkEntityId,
    string? AppraisalNumber = null);
