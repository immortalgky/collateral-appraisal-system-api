using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetTaskSummary;

public record GetTaskSummaryQuery(
    string Period,
    DateOnly? From,
    DateOnly? To
) : IQuery<GetTaskSummaryResult>;

public record GetTaskSummaryResult(List<TaskSummaryDto> Items);

public record TaskSummaryDto
{
    public string? Period { get; init; }
    public int NotStarted { get; init; }
    public int InProgress { get; init; }
    public int Overdue { get; init; }
    public int Completed { get; init; }
}
