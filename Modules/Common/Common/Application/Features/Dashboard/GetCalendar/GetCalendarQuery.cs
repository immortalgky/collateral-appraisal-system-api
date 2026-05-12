using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetCalendar;

public record GetCalendarQuery(
    DateOnly From,
    DateOnly To,
    string? Type = null
) : IQuery<GetCalendarResponse>;
