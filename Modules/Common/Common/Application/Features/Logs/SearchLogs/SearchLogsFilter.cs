namespace Common.Application.Features.Logs.SearchLogs;

public record SearchLogsFilter(
    string? Level,
    string? CorrelationId,
    string? AppraisalId,
    string? RequestId,
    string? EntityId,
    string? WorkflowInstanceId,
    string? CollateralId,
    string? DocumentId,
    string? Search,
    DateTime? From,
    DateTime? To,
    string? SortDir
);
