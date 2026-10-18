using System.Security.Claims;
using Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using Shared.Time;

namespace Auth.Application.Services;

/// <summary>
/// What the app session needs to know about a user on every request: whether the account may still be
/// used, whether it owes a password change, and the claims the access token would have carried.
/// </summary>
public sealed record UserAuthorizationSnapshot(
    bool IsActive,
    DateTime? AccessExpiresAt,
    bool MustChangePassword,
    IReadOnlyList<Claim> Claims,
    long LoadedAt = 0) // Stopwatch timestamp of the database read; how old the claims are
{
    public bool IsUsable(DateTime now) => ApplicationUser.IsUsable(IsActive, AccessExpiresAt, now);
}

/// <summary>
/// Builds <c>permissions</c>, <c>roles</c> and <c>company_id</c> for the browser session, from the same
/// <see cref="PermissionResolver"/> that backs <c>/auth/me</c>.
/// </summary>
/// <remarks>
/// The app cookie holds only identity, so these claims are added on every request. Without them the
/// failures are silent: audit stamps read "anonymous", every <c>IsInRole</c> is false, permission
/// policies return 403 and — worst — an external-company user is treated as internal staff because
/// <c>company_id</c> is missing, which drops their data filter. <c>TokenService</c> builds the same three
/// claims for Bearer tokens; the claims-parity test keeps the two from drifting apart.
/// </remarks>
public class AuthorizationClaimsBuilder(
    AuthDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    PermissionResolver permissionResolver,
    IPasswordPolicyProvider passwordPolicyProvider,
    IDateTimeProvider dateTimeProvider,
    IMemoryCache cache,
    ILogger<AuthorizationClaimsBuilder> logger)
{
    // A role or permission edit reaches a signed-in user within this long. Per node, like the menu cache.
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    private static string CacheKey(Guid userId) => $"auth:session:user:{userId:N}";

    /// <summary>Drops this node's cached snapshot so its next request sees the change; the other node within <c>CacheLifetime</c>.</summary>
    public void Invalidate(Guid userId) => cache.Remove(CacheKey(userId));

    /// <summary>
    /// Cache-only lookup. Lets the per-request caller skip resolving this class — a DbContext and an
    /// Identity UserManager — on the common path where the snapshot is still fresh.
    /// </summary>
    public static bool TryGetCached(IMemoryCache cache, Guid userId, out UserAuthorizationSnapshot? snapshot) =>
        cache.TryGetValue(CacheKey(userId), out snapshot);

    /// <summary>
    /// A cached snapshot that says the user owes a password change must not be trusted for the whole cache
    /// lifetime: the change is made through one node and the other would keep answering 403. Re-reads what decides
    /// it — the flag, and the expiry rule that can also set it — without reloading the claims. Only users who owe a
    /// change pay for this.
    /// </summary>
    /// <remarks>
    /// The expiry rule is re-evaluated, not just the flag, because the flag is persisted best-effort: if that
    /// write failed, the flag reads false while the password is still expired, and the gate must not flip off.
    /// When the change is no longer owed, the snapshot keeps its ORIGINAL expiry: its permissions and roles are
    /// no fresher than when they were loaded and must not get a new lifetime for it.
    /// </remarks>
    public async Task<UserAuthorizationSnapshot> RefreshMustChangePasswordAsync(Guid userId,
        UserAuthorizationSnapshot cached, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.MustChangePassword, u.AuthSource, u.PasswordChangedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null || user.MustChangePassword
            || await IsPasswordExpiredAsync(userId, user.AuthSource, user.PasswordChangedAt, cancellationToken))
            return cached;

        var cleared = cached with { MustChangePassword = false };
        var remaining = CacheLifetime - Stopwatch.GetElapsedTime(cached.LoadedAt);
        if (remaining > TimeSpan.Zero) cache.Set(CacheKey(userId), cleared, remaining);
        else cache.Remove(CacheKey(userId));
        return cleared;
    }

    /// <summary>
    /// Reads the user from the database and caches the result. Used on a cache miss, and for decisions that end a
    /// session: those must never rest on a cached copy that a change made through the other node (an extended access
    /// window, a reactivation) has not reached yet. A user who no longer exists is not cached: the session ends on
    /// that first read, so nothing polls with it again.
    /// </summary>
    public async Task<UserAuthorizationSnapshot?> ReloadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await LoadAsync(userId, cancellationToken);
        if (snapshot is null) cache.Remove(CacheKey(userId));
        else cache.Set(CacheKey(userId), snapshot, CacheLifetime);
        return snapshot;
    }

    /// <summary>Null when the user no longer exists.</summary>
    public async Task<UserAuthorizationSnapshot?> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Same shape as MeQueryHandler: user-level grants/denies need the Permissions graph loaded,
        // and PermissionResolver folds the role permissions in on top.
        var user = await dbContext.Users.AsNoTracking()
            .Include(u => u.Permissions)
            .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return null;

        var roles = await userManager.GetRolesAsync(user);
        var permissions = await permissionResolver.CalculateAsync(user, roles);

        var claims = new List<Claim>();
        if (user.CompanyId.HasValue)
            claims.Add(new Claim("company_id", user.CompanyId.Value.ToString()));
        claims.AddRange(permissions.Select(p => new Claim("permissions", p)));
        claims.AddRange(roles.Select(r => new Claim("roles", r)));

        var mustChangePassword = user.MustChangePassword
                                 || await IsPasswordExpiredAsync(user.Id, user.AuthSource, user.PasswordChangedAt,
                                     cancellationToken);
        return new UserAuthorizationSnapshot(user.IsActive, user.AccessExpiresAt, mustChangePassword, claims,
            Stopwatch.GetTimestamp());
    }

    // The password-expiry rule. Local-password accounts only — LDAP passwords are governed by AD, and legacy
    // accounts with no recorded change date are not force-expired (consistent with the login page). When it holds
    // the flag is also persisted (best-effort) so /auth/me tells the SPA to route to the change-password screen;
    // the gate itself does not depend on that write succeeding, because this rule is re-evaluated whenever the
    // flag is re-read.
    private async Task<bool> IsPasswordExpiredAsync(Guid userId, string? authSource, DateTime? passwordChangedAt,
        CancellationToken cancellationToken)
    {
        if (!AuthSources.IsLocal(authSource) || passwordChangedAt is not { } changedAt)
            return false;

        var policy = await passwordPolicyProvider.GetAsync(cancellationToken);
        if (policy.ExpiryDays <= 0 || changedAt.AddDays(policy.ExpiryDays) >= dateTimeProvider.ApplicationNow)
            return false;

        try
        {
            // Single column, not UserManager.UpdateAsync (its email-uniqueness validator would fail for a
            // pre-existing duplicate-email account).
            await dbContext.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, true), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist MustChangePassword on expiry for {UserId}", userId);
        }

        return true;
    }
}
