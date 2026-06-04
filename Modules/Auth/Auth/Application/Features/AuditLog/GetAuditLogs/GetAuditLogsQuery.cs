using Auth.Domain.Auditing;

namespace Auth.Application.Features.AuditLog.GetAuditLogs;

public record GetAuditLogsQuery(
    int PageNumber,
    int PageSize,
    AuditEntityType? EntityType,
    Guid? EntityId,
    Guid? ActorUserId,
    DateTime? From,
    DateTime? To,
    AuditAction? Action
) : IQuery<GetAuditLogsResult>;
