using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Auth.Infrastructure.Configuration;
using Auth.Infrastructure.Repository;
using OpenIddict.EntityFrameworkCore.Models;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Shared.Time;

namespace Auth.Application.Services;

public class TokenService(
    AuthDbContext openIddictDbContext,
    UserManager<ApplicationUser> userManager,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager,
    IRoleRepository roleRepository,
    IPasswordPolicyProvider passwordPolicyProvider,
    IDateTimeProvider dateTimeProvider,
    IOptionsMonitor<OpenIddictServerOptions> serverOptions,
    ILogger<TokenService> logger
) : ITokenService
{
    private static readonly Dictionary<string, string> ClaimScopeMapping = new()
    {
        { OpenIddictConstants.Claims.Name, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.FamilyName, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.GivenName, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.MiddleName, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.Nickname, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.PreferredUsername, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.Profile, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.Picture, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.Website, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.Gender, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.Birthdate, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.Zoneinfo, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.Locale, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.UpdatedAt, OpenIddictConstants.Scopes.Profile },
        { OpenIddictConstants.Claims.Email, OpenIddictConstants.Scopes.Email },
        { OpenIddictConstants.Claims.EmailVerified, OpenIddictConstants.Scopes.Email },
        { OpenIddictConstants.Claims.Address, OpenIddictConstants.Scopes.Address },
        { OpenIddictConstants.Claims.PhoneNumber, OpenIddictConstants.Scopes.Phone },
        { OpenIddictConstants.Claims.PhoneNumberVerified, OpenIddictConstants.Scopes.Phone }
    };

    public async Task<TokenGrantResult> CreateAuthCodeFlowAccessTokenPrincipal(
        OpenIddictRequest request,
        ClaimsPrincipal principal
    )
    {
        // Get user info from the principal
        var userId =
            principal.FindFirstValue(OpenIddictConstants.Claims.Subject)
            ?? throw new InvalidOperationException("Cannot find user ID associated with the token");
        var username =
            principal.FindFirstValue(OpenIddictConstants.Claims.Name)
            ?? throw new InvalidOperationException(
                "Cannot find user name associated with the token"
            );

        var userIdGuid = Guid.Parse(userId); // Can throws exception
        var user = await LoadUserWithPermissionsAsync(userIdGuid)
            ?? throw new InvalidOperationException("Cannot find user associated with the token.");

        // The authorization code was minted from the Identity cookie, which survives both
        // deactivation and the end of an access window — so without this check either could be
        // walked around simply by holding an open browser session.
        if (!user.IsUsable(dateTimeProvider.ApplicationNow))
            return new TokenGrantResult(null, "This account is no longer active. Please sign in again.");

        return new TokenGrantResult(await BuildAccessTokenPrincipal(request, userId, username, user), null);
    }

    private Task<ApplicationUser?> LoadUserWithPermissionsAsync(Guid userId) =>
        openIddictDbContext
            .Users.Include(u => u.Permissions)
            .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId);

    // Builds the access-token principal from an already-loaded user. Kept separate from the DB
    // load so the refresh flow can validate + build from a single user fetch.
    // scopesFallback: when provided (refresh flow), used instead of request.GetScopes() if the
    // request carries no scopes — refresh requests typically omit scope, which would otherwise
    // produce an empty scope set and strip permission destinations from the new token.
    private async Task<ClaimsPrincipal> BuildAccessTokenPrincipal(
        OpenIddictRequest request,
        string userId,
        string username,
        ApplicationUser user,
        IEnumerable<string>? scopesFallback = null)
    {
        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // Add basic claims
        identity.AddClaim(OpenIddictConstants.Claims.Subject, userId);
        identity.AddClaim(OpenIddictConstants.Claims.Name, username);

        // Add profile claims
        identity.AddClaim(OpenIddictConstants.Claims.GivenName, user.FirstName ?? string.Empty);
        identity.AddClaim(OpenIddictConstants.Claims.FamilyName, user.LastName ?? string.Empty);
        identity.AddClaim(OpenIddictConstants.Claims.Email, user.Email ?? string.Empty);
        identity.AddClaim(OpenIddictConstants.Claims.PreferredUsername, user.UserName ?? string.Empty);

        // Add company claim
        if (user.CompanyId.HasValue)
            identity.AddClaim("company_id", user.CompanyId.Value.ToString());

        // Temporary-access accounts carry the end of their window in the token, so the SPA can warn
        // and sign out on time instead of discovering the window closed through a 401 mid-task.
        // Local time on purpose: the whole system runs on bank-local time (see IDateTimeProvider).
        if (user.AccessExpiresAt is not null)
            identity.AddClaim("access_expires_at", user.AccessExpiresAt.Value.ToString("s"));

        // Add permissions and roles (original approach)
        identity.SetClaims("permissions", await GetUserPermissions(user));
        identity.SetClaims("roles", [.. await userManager.GetRolesAsync(user)]);

        // Resolve effective scopes: use the request's scopes when present (auth-code flow), or fall
        // back to the caller-supplied scopesFallback (refresh flow — refresh requests typically omit
        // the scope parameter, which would otherwise produce an empty set and strip permission
        // destinations from the newly-issued access token).
        var requestScopes = request.GetScopes();
        var effectiveScopes = requestScopes.Any() ? requestScopes : (scopesFallback ?? []);

        // Set scopes first, then destinations (order matters for GetDestinations)
        identity.SetScopes(effectiveScopes);
        identity.SetDestinations(GetDestinations);

        var claimsPrincipal = new ClaimsPrincipal(identity);
        claimsPrincipal.SetScopes(effectiveScopes);

        // An access token is a bearer credential nothing re-checks until it expires, so a token that
        // outlives the access window would keep the API open after the window shut. Shorten this one
        // to whatever is left — never lengthen it: the client's own configured lifetime stays the ceiling.
        if (user.AccessExpiresAt is { } windowEnd)
        {
            var remaining = windowEnd - dateTimeProvider.ApplicationNow;
            var ceiling = await ResolveAccessTokenLifetimeAsync(request.ClientId);
            if (remaining > TimeSpan.Zero && remaining < ceiling)
                claimsPrincipal.SetAccessTokenLifetime(remaining);
        }

        // Deliberately does NOT call SetRefreshTokenLifetime. A lifetime attached to the principal
        // outranks everything else in OpenIddict, which would silently shadow the per-client value
        // stored on the application row — an admin could edit it in /admin/clients and see no effect.
        // Session length is owned by that setting; see AuthModule for the server-wide fallback.
        return claimsPrincipal;
    }

    /// <summary>
    /// The access-token lifetime this client would normally get: its own per-application setting when
    /// it has one, otherwise the server-wide default configured in AuthModule. Only ever used as an
    /// upper bound, so a missing or unparsable value simply leaves the normal lifetime in force.
    /// </summary>
    private async Task<TimeSpan> ResolveAccessTokenLifetimeAsync(string? clientId)
    {
        // Read the server-wide default from OpenIddict's own options rather than restating the number
        // AuthModule configures: a copy here would silently stop shortening tokens the day someone
        // raises that setting, which is exactly what this clamp exists to prevent.
        var fallback = serverOptions.CurrentValue.AccessTokenLifetime ?? TimeSpan.FromMinutes(15);
        if (string.IsNullOrEmpty(clientId))
            return fallback;

        var application = await applicationManager.FindByClientIdAsync(clientId);
        if (application is null)
            return fallback;

        var settings = await applicationManager.GetSettingsAsync(application);
        return settings.TryGetValue(OpenIddictConstants.Settings.TokenLifetimes.AccessToken, out var raw)
               && TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var configured)
               && configured > TimeSpan.Zero
            ? configured
            : fallback;
    }

    public async Task<ClaimsPrincipal> CreateClientCredFlowAccessTokenPrincipal(
        OpenIddictRequest request
    )
    {
        var clientId =
            request.ClientId
            ?? throw new InvalidOperationException("The client ID cannot be found in the request.");
        var application = (OpenIddictEntityFrameworkCoreApplication)(
            await applicationManager.FindByClientIdAsync(clientId)
            ?? throw new InvalidOperationException("The application details cannot be found.")
        );

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role
        );

        identity.SetClaim(
            OpenIddictConstants.Claims.Subject,
            await applicationManager.GetClientIdAsync(application)
        );
        identity.SetClaim(
            OpenIddictConstants.Claims.Name,
            await applicationManager.GetDisplayNameAsync(application)
        );
        identity.SetClaim("aud", "resource_server");
        identity.SetClaims(
            "permissions",
            await applicationManager.GetPermissionsAsync(application)
        );

        identity.SetScopes(request.GetScopes());
        identity.SetResources(
            await scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync()
        );

        identity.SetDestinations(GetDestinations);

        var claimsPrincipal = new ClaimsPrincipal(identity);
        return claimsPrincipal;
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        // Subject, Name and PreferredUsername always go to both tokens. The API authenticates with
        // the ACCESS token and reads preferred_username (the bank code, e.g. "P5229") to stamp the
        // acting user on audit/actor fields, so it must be present there — not the id-token only.
        if (claim.Type is OpenIddictConstants.Claims.Subject
            or OpenIddictConstants.Claims.Name
            or OpenIddictConstants.Claims.PreferredUsername)
            return [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken];

        // Other profile claims (email, given_name, etc.) go to ID Token only (OIDC standard)
        if (ClaimScopeMapping.ContainsKey(claim.Type))
            return claim.Subject!.HasScope(ClaimScopeMapping[claim.Type])
                ? [OpenIddictConstants.Destinations.IdentityToken]
                : [];

        // Authorization claims (permissions, roles) go to Access Token only
        return [OpenIddictConstants.Destinations.AccessToken];
    }

    /// <summary>
    /// Re-validates the account on every refresh-token exchange so account state (deactivation,
    /// forced password change, password expiry) takes effect within one access-token lifetime
    /// instead of lingering for the full refresh-token lifetime, and — when the refresh may
    /// proceed — builds the new access-token principal from the SAME user load (no second query).
    /// </summary>
    public async Task<TokenGrantResult> CreateRefreshFlowPrincipalAsync(
        OpenIddictRequest request,
        ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(OpenIddictConstants.Claims.Subject);
        if (!Guid.TryParse(userId, out var id))
            return new TokenGrantResult(null, "The refresh token is no longer valid.");

        var user = await LoadUserWithPermissionsAsync(id);
        // Covers both deactivation and a temporary account whose access window has run out.
        if (user is null || !user.IsUsable(dateTimeProvider.ApplicationNow))
            return new TokenGrantResult(null, "This account is no longer active. Please sign in again.");

        if (user.MustChangePassword)
            return new TokenGrantResult(null, "A password change is required. Please sign in again to continue.");

        // Local-password accounts only — LDAP passwords are governed by AD, and legacy accounts with
        // no recorded change date are not force-expired (consistent with the login page).
        if (AuthSources.IsLocal(user.AuthSource) && user.PasswordChangedAt is { } changedAt)
        {
            var policy = await passwordPolicyProvider.GetAsync();
            if (policy.ExpiryDays > 0
                && changedAt.AddDays(policy.ExpiryDays) < dateTimeProvider.ApplicationNow)
            {
                // Persist the flag so the next interactive login routes the user to change password.
                // Write the single column directly (not via UserManager.UpdateAsync, which runs the
                // email-uniqueness validator and would fail for a pre-existing duplicate-email account).
                // Best-effort: a transient/concurrency failure here must NOT fault the grant — the
                // rejection below is the load-bearing behavior (mirrors Login.StampLastLoginAsync).
                user.MustChangePassword = true;
                try
                {
                    await openIddictDbContext.Users
                        .Where(u => u.Id == user.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, true));
                }
                catch (Exception ex)
                {
                    // Flag not flipped this round; the refresh is still rejected and the next
                    // interactive login re-evaluates expiry and flips it.
                    logger.LogWarning(ex, "Failed to persist MustChangePassword on expiry for {UserId} — refresh still rejected", user.Id);
                }
                return new TokenGrantResult(null, "Your password has expired. Please sign in again to set a new one.");
            }
        }

        var username = principal.FindFirstValue(OpenIddictConstants.Claims.Name) ?? user.UserName ?? string.Empty;
        // Pass the existing principal's scopes as fallback so that refresh requests omitting the
        // optional scope parameter carry forward the original grant's scopes rather than losing them.
        var built = await BuildAccessTokenPrincipal(request, id.ToString(), username, user,
            scopesFallback: principal.GetScopes());
        return new TokenGrantResult(built, null);
    }

    internal async Task<ImmutableArray<string>> GetUserPermissions(ApplicationUser user)
    {
        var roleNames = await userManager.GetRolesAsync(user);
        return await CalcUserPermissions(user.Permissions, roleNames, roleRepository);
    }

    public static async Task<ImmutableArray<string>> CalcUserPermissions(IList<UserPermission> userPermissions,
        IList<string> roleNames, IRoleRepository roleRepository)
    {
        var permissions = new HashSet<string>();
        var ungrantedPermissions = new HashSet<string>();

        foreach (var permission in userPermissions)
            if (permission.IsGranted)
                permissions.Add(permission.Permission.PermissionCode);
            else
                ungrantedPermissions.Add(permission.Permission.PermissionCode);

        foreach (var roleName in roleNames)
        {
            var role = await roleRepository.GetRoleByName(roleName)
                       ?? throw new InvalidOperationException($"Cannot find role named {roleName}");

            foreach (
                var allowedRolePermission in role.Permissions.Where(rolePermission =>
                    !ungrantedPermissions.Contains(rolePermission.Permission.PermissionCode)
                )
            )
                permissions.Add(allowedRolePermission.Permission.PermissionCode);
        }

        return [.. permissions];
    }
}