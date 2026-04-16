namespace Common.Application.Features.Dashboard.GetReminders;

public sealed record GetRemindersResponse(List<ReminderDto> Items);

public sealed record ReminderDto(
    Guid Id,
    string Type,
    string Title,
    string? AppraisalNumber,
    DateTimeOffset? DueAt,
    bool Overdue);
