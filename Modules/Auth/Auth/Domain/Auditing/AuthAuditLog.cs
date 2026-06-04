using Shared.DDD;

namespace Auth.Domain.Auditing;

/// <summary>
/// Append-only audit record for mutations in the Auth module.
/// Does NOT implement IAggregate / raise domain events — plain entity only.
/// </summary>
public class AuthAuditLog : Entity<Guid>
{
    public DateTime OccurredAt { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string? ActorName { get; private set; }
    public AuditAction Action { get; private set; }
    public AuditEntityType EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public string? EntityName { get; private set; }
    public string? ChangesJson { get; private set; }
    public string? Workstation { get; private set; }
    public string? IpAddress { get; private set; }

    private AuthAuditLog() { }

    public static AuthAuditLog Create(
        DateTime occurredAt,
        Guid? actorUserId,
        string? actorName,
        AuditAction action,
        AuditEntityType entityType,
        Guid? entityId,
        string? entityName,
        string? changesJson = null,
        string? workstation = null,
        string? ipAddress = null)
    {
        return new AuthAuditLog
        {
            Id = Guid.CreateVersion7(),
            OccurredAt = occurredAt,
            ActorUserId = actorUserId,
            ActorName = actorName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = entityName,
            ChangesJson = changesJson,
            Workstation = workstation,
            IpAddress = ipAddress
        };
    }
}
