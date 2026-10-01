namespace Common.Application.Features.Logs.SearchLogs;

public record SearchLogsFilter(
    string? Q,
    DateTime? From,
    DateTime? To,
    string? Levels,
    long? BeforeId,
    long? AfterId,
    int? PageSize,
    string? SortDir
);
