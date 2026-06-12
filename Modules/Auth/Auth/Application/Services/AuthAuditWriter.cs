using System.Text.Json;
using Auth.Domain.Auditing;
using Auth.Infrastructure;
using Microsoft.AspNetCore.Http;
using Shared.Identity;
using Shared.Time;

namespace Auth.Application.Services;

/// <summary>
/// Enqueues AuthAuditLog rows into the EF change tracker.
/// NEVER calls SaveChanges — callers flush atomically via their existing SaveChangesAsync.
/// </summary>
public class AuthAuditWriter(
    AuthDbContext dbContext,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IHttpContextAccessor httpContextAccessor) : IAuthAuditWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Record(
        AuditAction action,
        AuditEntityType entityType,
        Guid? entityId,
        string? entityName,
        object? changes = null)
    {
        var log = AuthAuditLog.Create(
            occurredAt: dateTimeProvider.ApplicationNow,
            actorUserId: currentUserService.UserId,
            actorName: currentUserService.Username ?? "system",
            action: action,
            entityType: entityType,
            entityId: entityId,
            entityName: entityName,
            changesJson: changes is null ? null : JsonSerializer.Serialize(changes, JsonOptions),
            workstation: null,
            ipAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());

        dbContext.AuthAuditLogs.Add(log);
    }

    public void RecordAssignmentChange(
        AuditEntityType entityType,
        Guid entityId,
        string? entityName,
        IEnumerable<Guid> before,
        IEnumerable<Guid> after,
        string setName)
    {
        var beforeList = before.ToList();
        var afterList = after.ToList();

        var added = afterList.Except(beforeList).ToList();
        var removed = beforeList.Except(afterList).ToList();

        var changes = new { setName, added, removed };

        var log = AuthAuditLog.Create(
            occurredAt: dateTimeProvider.ApplicationNow,
            actorUserId: currentUserService.UserId,
            actorName: currentUserService.Username ?? "system",
            action: AuditAction.AssignmentChanged,
            entityType: entityType,
            entityId: entityId,
            entityName: entityName,
            changesJson: JsonSerializer.Serialize(changes, JsonOptions),
            workstation: null,
            ipAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());

        dbContext.AuthAuditLogs.Add(log);
    }

    public async Task RecordAuthEventAsync(
        AuditAction action,
        Guid? userId,
        string? username,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        var log = AuthAuditLog.Create(
            occurredAt: dateTimeProvider.ApplicationNow,
            actorUserId: userId,
            actorName: username ?? "anonymous",
            action: action,
            entityType: AuditEntityType.User,
            entityId: userId,
            entityName: username,
            changesJson: details is null ? null : JsonSerializer.Serialize(details, JsonOptions),
            workstation: null,
            ipAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());

        dbContext.AuthAuditLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public void RecordAssignmentChange(
        AuditEntityType entityType,
        Guid entityId,
        string? entityName,
        IEnumerable<string> before,
        IEnumerable<string> after,
        string setName)
    {
        var beforeList = before.ToList();
        var afterList = after.ToList();

        var added = afterList.Except(beforeList, StringComparer.OrdinalIgnoreCase).ToList();
        var removed = beforeList.Except(afterList, StringComparer.OrdinalIgnoreCase).ToList();

        var changes = new { setName, added, removed };

        var log = AuthAuditLog.Create(
            occurredAt: dateTimeProvider.ApplicationNow,
            actorUserId: currentUserService.UserId,
            actorName: currentUserService.Username ?? "system",
            action: AuditAction.AssignmentChanged,
            entityType: entityType,
            entityId: entityId,
            entityName: entityName,
            changesJson: JsonSerializer.Serialize(changes, JsonOptions),
            workstation: null,
            ipAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString());

        dbContext.AuthAuditLogs.Add(log);
    }
}
