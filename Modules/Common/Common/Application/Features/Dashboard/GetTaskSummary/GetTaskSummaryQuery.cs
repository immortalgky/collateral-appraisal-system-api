using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetTaskSummary;

public record GetTaskSummaryQuery(
    DateOnly? From = null,
    DateOnly? To = null
) : IQuery<GetTaskSummaryResult>;

public record GetTaskSummaryResult(
    int NotStarted,
    int InProgress,
    int Overdue,
    int Completed
);
