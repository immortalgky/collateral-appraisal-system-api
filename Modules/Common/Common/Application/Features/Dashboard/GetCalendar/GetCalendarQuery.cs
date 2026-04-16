using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetCalendar;

public record GetCalendarQuery(string Month) : IQuery<GetCalendarResponse>;
