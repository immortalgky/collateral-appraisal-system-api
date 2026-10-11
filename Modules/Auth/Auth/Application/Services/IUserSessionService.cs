using Auth.Domain.Sessions;
using Shared.Identity;

namespace Auth.Application.Services;

/// <summary>What the session store needs from a live <c>auth.UserSessions</c> row.</summary>
public sealed record SessionSnapshot(Guid UserId, byte[] Ticket, DateTime LastActivityAt);

/// <summary>Reads and ends browser sessions. Also answers SignalR's "is this connection's session still alive?".</summary>
public interface IUserSessionService : ISessionStatusReader
{
    Task CreateAsync(Guid id, Guid userId, string? userName, byte[] ticket, string? ip, string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>The row if it exists and has not ended; idle expiry is the caller's decision.</summary>
    Task<SessionSnapshot?> GetActiveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Records activity. False when the session has ended in the meantime.</summary>
    Task<bool> TouchAsync(Guid id, DateTime now, CancellationToken cancellationToken = default);

    Task RenewTicketAsync(Guid id, byte[] ticket, CancellationToken cancellationToken = default);

    /// <summary>Ends one session. False when it had already ended (so the audit entry is written once).</summary>
    Task<bool> EndAsync(Guid id, SessionEndReason reason, DateTime? endedAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends every live session of a user, except <paramref name="exceptSessionId"/>. The audit entry is only
    /// enqueued: the caller's own SaveChanges (or the transactional behavior) flushes it. Returns the ids of the
    /// sessions it looked at; a caller running inside a transaction passes them to <see cref="Evict"/> after commit,
    /// because the eviction done here happens before it.
    /// </summary>
    Task<IReadOnlyList<Guid>> RevokeUserAsync(Guid userId, string? userName, SessionEndReason reason, Guid? exceptSessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Drops this node's cached copies of these sessions; the other node's expire within <c>SessionTicketStore.CacheLifetime</c>.</summary>
    void Evict(IEnumerable<Guid> sessionIds);
}
