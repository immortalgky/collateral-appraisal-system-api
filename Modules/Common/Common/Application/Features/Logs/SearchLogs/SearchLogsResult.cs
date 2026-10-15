namespace Common.Application.Features.Logs.SearchLogs;

public record SearchLogsResult(IReadOnlyList<LogListItem> Items, bool HasMore);
