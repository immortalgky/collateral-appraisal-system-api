namespace Auth.Application.Features.AuditLog.GetAuditLogs;

public record AuditLogItemDto(
    Guid Id,
    DateTime OccurredAt,
    Guid? ActorUserId,
    string? ActorName,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? EntityName,
    string? ChangesJson,
    string? IpAddress);

public record GetAuditLogsResult(
    IReadOnlyList<AuditLogItemDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
