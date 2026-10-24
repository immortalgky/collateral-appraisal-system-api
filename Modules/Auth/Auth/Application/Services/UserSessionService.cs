using Auth.Domain.Auditing;
using Auth.Domain.Sessions;
using Auth.Infrastructure.Session;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Shared.Time;

namespace Auth.Application.Services;

public class UserSessionService(
    AuthDbContext dbContext,
    IAuthAuditWriter auditWriter,
    IDateTimeProvider dateTimeProvider,
    IMemoryCache cache,
    IOptions<BrowserSessionOptions> options) : IUserSessionService
{
    /// <summary>Cache key the session store uses for a session; evicted here whenever a row changes.</summary>
    public static string CacheKey(Guid sessionId) => $"auth:session:{sessionId:N}";

    public async Task CreateAsync(Guid id, Guid userId, string? userName, byte[] ticket, string? ip,
        string? userAgent, CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.ApplicationNow;
        dbContext.UserSessions.Add(new UserSession
        {
            Id = id,
            UserId = userId,
            Ticket = ticket,
            CreatedAt = now,
            LastActivityAt = now,
            Ip = ip,
            UserAgent = userAgent is { Length: > 512 } ? userAgent[..512] : userAgent
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.RecordAuthEventAsync(AuditAction.SessionStarted, userId, userName,
            new { sessionId = id }, cancellationToken);
    }

    public Task<SessionSnapshot?> GetActiveAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.UserSessions.AsNoTracking()
            .Where(s => s.Id == id && s.EndedAt == null)
            .Select(s => new SessionSnapshot(s.UserId, s.Ticket, s.LastActivityAt))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<SessionEndReason?> GetEndReasonAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.UserSessions.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => s.EndReason)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TouchAsync(Guid id, DateTime now, CancellationToken cancellationToken = default) =>
        await dbContext.UserSessions
            .Where(s => s.Id == id && s.EndedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastActivityAt, now), cancellationToken) > 0;

    public async Task RenewTicketAsync(Guid id, byte[] ticket, CancellationToken cancellationToken = default)
    {
        await dbContext.UserSessions
            .Where(s => s.Id == id && s.EndedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Ticket, ticket), cancellationToken);
        cache.Remove(CacheKey(id));
    }

    public async Task<bool> EndAsync(Guid id, SessionEndReason reason, DateTime? endedAt = null,
        CancellationToken cancellationToken = default)
    {
        cache.Remove(CacheKey(id));
        var userId = await dbContext.UserSessions.AsNoTracking()
            .Where(s => s.Id == id && s.EndedAt == null)
            .Select(s => (Guid?)s.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (userId is null) return false;

        var when = endedAt ?? dateTimeProvider.ApplicationNow;
        var ended = await dbContext.UserSessions
            .Where(s => s.Id == id && s.EndedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.EndedAt, when)
                .SetProperty(x => x.EndReason, (SessionEndReason?)reason), cancellationToken) > 0;
        // Another node may have won the race (idle expiry is detected by whoever sees it first).
        if (!ended) return false;

        var userName = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync(cancellationToken);
        await auditWriter.RecordAuthEventAsync(AuditAction.SessionEnded, userId, userName,
            new { sessionId = id, reason = reason.ToString() }, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<Guid>> RevokeUserAsync(Guid userId, string? userName, SessionEndReason reason,
        Guid? exceptSessionId = null, CancellationToken cancellationToken = default)
    {
        var except = exceptSessionId ?? Guid.Empty;
        var open = await dbContext.UserSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.EndedAt == null && s.Id != except)
            .Select(s => new { s.Id, s.LastActivityAt })
            .ToListAsync(cancellationToken);
        if (open.Count == 0) return [];

        var now = dateTimeProvider.ApplicationNow;
        var idle = options.Value.IdleTimeout;

        // Only a session that was still live gets the reason: an open row nobody has touched for longer than the
        // idle timeout was already over, and recording it as "deactivated" or "password reset" would blame the
        // admin action for something that had happened by itself. It ends as Idle, at the moment it went idle.
        var live = open.Where(s => now - s.LastActivityAt < idle).Select(s => s.Id).ToList();
        if (live.Count > 0)
            await dbContext.UserSessions
                .Where(s => live.Contains(s.Id) && s.EndedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.EndedAt, now)
                    .SetProperty(x => x.EndReason, (SessionEndReason?)reason), cancellationToken);

        // ponytail: one UPDATE per stale row; a user has a handful of sessions. Batch if that ever stops being true.
        foreach (var stale in open.Where(s => now - s.LastActivityAt >= idle))
        {
            var wentIdleAt = stale.LastActivityAt + idle;
            var id = stale.Id;
            await dbContext.UserSessions
                .Where(s => s.Id == id && s.EndedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.EndedAt, wentIdleAt)
                    .SetProperty(x => x.EndReason, (SessionEndReason?)SessionEndReason.Idle), cancellationToken);
        }

        // Right for callers outside a transaction (the UPDATEs above autocommitted); a transactional caller
        // evicts again after commit, see Evict.
        Evict(open.Select(s => s.Id));

        if (live.Count > 0)
            auditWriter.Record(AuditAction.SessionEnded, AuditEntityType.User, userId, userName,
                new { reason = reason.ToString(), count = live.Count });

        return open.Select(s => s.Id).ToList();
    }

    public void Evict(IEnumerable<Guid> sessionIds)
    {
        foreach (var id in sessionIds) cache.Remove(CacheKey(id));
    }

    public async Task<IReadOnlySet<Guid>> GetEndedAsync(IReadOnlyCollection<Guid> sessionIds,
        CancellationToken cancellationToken)
    {
        if (sessionIds.Count == 0) return new HashSet<Guid>();

        var idleCutoff = dateTimeProvider.ApplicationNow - options.Value.IdleTimeout;
        var alive = await dbContext.UserSessions.AsNoTracking()
            .Where(s => sessionIds.Contains(s.Id) && s.EndedAt == null && s.LastActivityAt > idleCutoff)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        return sessionIds.Except(alive).ToHashSet();
    }
}
