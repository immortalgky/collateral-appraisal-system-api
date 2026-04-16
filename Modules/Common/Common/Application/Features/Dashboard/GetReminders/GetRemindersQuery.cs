using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetReminders;

public record GetRemindersQuery : IQuery<GetRemindersResponse>;
