using System.Security.Claims;
using Auth.Application.Services;
using Auth.Domain.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Identity;
using Shared.Time;

namespace Auth.Infrastructure.Session;

/// <summary>
/// Keeps the authentication ticket in <c>auth.UserSessions</c> instead of the cookie. The cookie then
/// holds only a protected session id, so logging out, revoking or an idle timeout really end the
/// session on the server — a copied cookie stops working with it.
/// </summary>
/// <remarks>
/// Registered as a singleton (the cookie options are), so every database touch opens its own scope.
/// A row is cached for at most <c>CacheLifetime</c> per node so a page firing ten XHRs reads it once; the
/// cost is that a revoke made on the other node reaches this one up to that long later.
/// </remarks>
public sealed class SessionTicketStore(
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache,
    IDataProtectionProvider dataProtection,
    IHttpContextAccessor httpContextAccessor,
    IOptions<BrowserSessionOptions> options,
    IDateTimeProvider dateTimeProvider,
    ILogger<SessionTicketStore> logger) : ITicketStore
{
    internal static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(10);

    // Activity is written at most this often per session; a busy user would otherwise cost one UPDATE per request.
    private static readonly TimeSpan ActivityWriteInterval = TimeSpan.FromMinutes(1);

    private readonly IDataProtector _protector = dataProtection.CreateProtector("Auth.SessionTicket.v1");

    /// <summary>"No live session has this id" — remembered briefly so a dead cookie that keeps being sent (a tab
    /// polling after the session ended) does not cost a database read per request.</summary>
    private sealed record MissingSession(DateTime CheckedAt);

    private sealed record CachedSession(Guid UserId, byte[] Ticket, DateTime LastActivityAt, DateTime CheckedAt);

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        if (!Guid.TryParse(ticket.Principal.FindFirstValue("sub"), out var userId))
            throw new InvalidOperationException("Cannot start a session for a principal without a 'sub' claim.");

        var id = Guid.CreateVersion7();
        var key = id.ToString("N");
        // The id travels inside the principal so request-time code (password change, SignalR) can tell
        // which session it is running in without another lookup.
        ticket.Principal.Identities.First().AddClaim(new Claim(AuthSchemes.SessionIdClaim, key));

        var request = httpContextAccessor.HttpContext?.Request;
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUserSessionService>().CreateAsync(
            id, userId, ticket.Principal.GetUserCode(), Protect(ticket),
            httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            request?.Headers.UserAgent.ToString());
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        if (!Guid.TryParse(key, out var id)) return;
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUserSessionService>().RenewTicketAsync(id, Protect(ticket));
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (!Guid.TryParse(key, out var id)) return null;

        var now = dateTimeProvider.ApplicationNow;
        var cacheKey = UserSessionService.CacheKey(id);

        // The cache holds what the database said a few seconds ago. It is only ever (re)filled by a database
        // read — never refreshed by a hit — so an entry cannot outlive CacheLifetime however busy the session
        // is, and a logout or revoke made on the other node is seen within that time.
        CachedSession? session = null;
        if (cache.TryGetValue(cacheKey, out object? entry))
        {
            if (entry is MissingSession missing && now - missing.CheckedAt < CacheLifetime) return null;
            session = entry as CachedSession;
        }

        var fromCache = session is not null && now - session.CheckedAt < CacheLifetime;
        if (!fromCache)
        {
            session = await LoadAsync(id, now);
            if (session is null) return null;
        }

        if (now - session!.LastActivityAt >= options.Value.IdleTimeout)
        {
            // The activity time may be stale if the other node has served this session since; only a fresh
            // read may end it.
            if (fromCache)
            {
                session = await LoadAsync(id, now);
                if (session is null) return null;
            }

            if (now - session.LastActivityAt >= options.Value.IdleTimeout)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IUserSessionService>()
                    .EndAsync(id, SessionEndReason.Idle, session.LastActivityAt + options.Value.IdleTimeout);
                cache.Set(cacheKey, new MissingSession(now), CacheLifetime);
                return null;
            }
        }

        // Background polling must not keep a session alive (D1), and activity is written at most once
        // a minute. A failed touch means the row ended after we read it. A successful touch is itself a
        // fresh confirmation from the database, so it may refill the cache.
        if (!IsBackgroundRequest() && now - session.LastActivityAt >= ActivityWriteInterval)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            if (!await scope.ServiceProvider.GetRequiredService<IUserSessionService>().TouchAsync(id, now))
            {
                cache.Set(cacheKey, new MissingSession(now), CacheLifetime);
                return null;
            }

            session = session with { LastActivityAt = now, CheckedAt = now };
            cache.Set(cacheKey, session, CacheLifetime);
            // At most once a minute per session, so cheap to keep: says which request counted as activity.
            if (logger.IsEnabled(LogLevel.Debug) && httpContextAccessor.HttpContext is { } counted)
                logger.LogDebug("Session {SessionId} activity from {Method} {Path}", id, counted.Request.Method,
                    counted.Request.Path.Value);
        }

        try
        {
            return Unprotect(session.Ticket);
        }
        catch (Exception ex)
        {
            // A ticket sealed by a key that is no longer in the ring. The session is unusable; the user signs in again.
            logger.LogWarning(ex, "Session {SessionId} holds a ticket that can no longer be unprotected", id);
            return null;
        }
    }

    private async Task<CachedSession?> LoadAsync(Guid id, DateTime now)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var found = await scope.ServiceProvider.GetRequiredService<IUserSessionService>().GetActiveAsync(id);
        if (found is null)
        {
            cache.Set(UserSessionService.CacheKey(id), new MissingSession(now), CacheLifetime);
            return null;
        }

        var session = new CachedSession(found.UserId, found.Ticket, found.LastActivityAt, now);
        cache.Set(UserSessionService.CacheKey(id), session, CacheLifetime);
        return session;
    }

    public async Task RemoveAsync(string key)
    {
        if (!Guid.TryParse(key, out var id)) return;

        var reason = httpContextAccessor.HttpContext?.Items[AuthSchemes.EndReasonItem] is SessionEndReason r
            ? r
            : SessionEndReason.Logout;
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUserSessionService>().EndAsync(id, reason);
    }

    private bool IsBackgroundRequest() =>
        httpContextAccessor.HttpContext is { } http && BackgroundRequests.IsBackground(http.Request);

    private byte[] Protect(AuthenticationTicket ticket) => _protector.Protect(TicketSerializer.Default.Serialize(ticket));

    private AuthenticationTicket? Unprotect(byte[] protectedTicket) =>
        TicketSerializer.Default.Deserialize(_protector.Unprotect(protectedTicket));
}
