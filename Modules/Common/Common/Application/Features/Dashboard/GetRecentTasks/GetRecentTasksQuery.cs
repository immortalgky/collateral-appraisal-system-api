using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetRecentTasks;

public record GetRecentTasksQuery(int Limit = 10) : IQuery<GetRecentTasksResult>;

public record GetRecentTasksResult(List<RecentTaskDto> Items);

public record RecentTaskDto
{
    public Guid Id { get; init; }
    public string? AppraisalNumber { get; init; }
    public string? CustomerName { get; init; }
    public string? TaskType { get; init; }
    public string? Purpose { get; init; }
    public string? Status { get; init; }
    public DateTime? RequestedAt { get; init; }
}
