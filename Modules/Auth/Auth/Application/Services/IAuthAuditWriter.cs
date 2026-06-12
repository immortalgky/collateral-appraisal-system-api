using Auth.Domain.Auditing;

namespace Auth.Application.Services;

public interface IAuthAuditWriter
{
    /// <summary>
    /// Enqueues an audit entry for a create/update/delete on a single entity.
    /// Only calls dbContext.Add — never SaveChanges.
    /// </summary>
    void Record(
        AuditAction action,
        AuditEntityType entityType,
        Guid? entityId,
        string? entityName,
        object? changes = null);

    /// <summary>
    /// Enqueues an audit entry for a membership/assignment set replacement (Guid-keyed).
    /// Computes added = after.Except(before), removed = before.Except(after).
    /// Only calls dbContext.Add — never SaveChanges.
    /// </summary>
    void RecordAssignmentChange(
        AuditEntityType entityType,
        Guid entityId,
        string? entityName,
        IEnumerable<Guid> before,
        IEnumerable<Guid> after,
        string setName);

    /// <summary>
    /// Overload for string-keyed assignment sets (e.g. role names).
    /// </summary>
    void RecordAssignmentChange(
        AuditEntityType entityType,
        Guid entityId,
        string? entityName,
        IEnumerable<string> before,
        IEnumerable<string> after,
        string setName);

    /// <summary>
    /// Records a self-action authentication event (login / failed login / logout) and
    /// flushes it immediately. Unlike Record/RecordAssignmentChange, the actor is passed
    /// explicitly (the principal is not yet established at login time) and this method
    /// calls SaveChanges itself, since auth events have no surrounding transaction to
    /// piggyback on. The audited entity is always the user (EntityType.User).
    /// </summary>
    Task RecordAuthEventAsync(
        AuditAction action,
        Guid? userId,
        string? username,
        object? details = null,
        CancellationToken cancellationToken = default);
}
