namespace Common.Application.Features.Logs.SearchLogs;

/// <summary>
/// Positional record — column order must match the SELECT list in SearchLogsQueryHandler.
/// </summary>
public record LogListItem(
    long Id,
    DateTime TimeStamp,
    string? Level,
    string? Message,
    string? Exception,
    string? CorrelationId,
    string? EntityId,
    string? AppraisalId,
    string? RequestId,
    string? WorkflowInstanceId,
    string? CollateralId,
    string? DocumentId,
    string? MachineName,
    string? UserName,
    string? SourceContext,
    string? RequestPath
);
