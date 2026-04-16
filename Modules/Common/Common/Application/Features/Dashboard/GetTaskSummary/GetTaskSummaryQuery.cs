using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetTaskSummary;

public record GetTaskSummaryQuery : IQuery<GetTaskSummaryResult>;

public record GetTaskSummaryResult(
    int NotStarted,
    int InProgress,
    int Overdue,
    int CompletedThisWeek
);
