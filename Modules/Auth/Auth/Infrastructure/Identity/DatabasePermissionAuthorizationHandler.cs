using Auth.Application.Services;
using Auth.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Auth.Infrastructure.Identity;

/// <summary>
/// Requires a permission code from a principal that carries no "permissions" claim.
/// </summary>
/// <remarks>
/// Every other permission policy is <c>RequireClaim("permissions", code)</c>, which works because the
/// SPA's access token carries the codes. The Identity.Application cookie does not: it is issued by the
/// interactive login page and holds only identity and role claims. Server-rendered pages that a browser
/// navigates to — the Hangfire dashboard — can only authenticate with that cookie, so for them the
/// permissions have to be read from the database instead.
/// </remarks>
public sealed class DatabasePermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}

/// <remarks>
/// Dependencies are resolved inside the handler, not injected. Every registered IAuthorizationHandler
/// is constructed on every authorized request — the handler set is an IEnumerable the provider takes
/// in its constructor — so constructor-injecting AuthDbContext, UserManager and PermissionResolver
/// would build an EF context and the whole Identity manager graph for requests that never raise this
/// requirement. Only /hangfire does. The cache lookup happens first and resolves nothing at all.
/// </remarks>
public sealed class DatabasePermissionAuthorizationHandler(
    IServiceProvider services,
    IMemoryCache cache)
    : AuthorizationHandler<DatabasePermissionRequirement>
{
    // The Hangfire dashboard polls for stats and pulls its own assets through this same policy, so
    // resolving from the database per request would be several queries a second for every open tab.
    // The trade-off of caching: a permission revoked in the admin screens takes up to this long to
    // take effect on such a page. Per-process, like the menu cache.
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DatabasePermissionRequirement requirement)
    {
        // Not signed in on the scheme this policy pinned — leave the requirement unmet and let the
        // authorization middleware challenge (which redirects the browser to the login page).
        // Read the claim directly rather than through UserManager.GetUserId: it is the same claim
        // (IdentityOptions.ClaimsIdentity.UserIdClaimType is left at its default here) and it keeps
        // the not-signed-in path from resolving anything.
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return;

        var permissions = await GetPermissionsAsync(userId);

        if (permissions.Contains(requirement.PermissionCode))
            context.Succeed(requirement);
    }

    private async Task<HashSet<string>> GetPermissionsAsync(Guid userId)
    {
        var cacheKey = $"auth:permissions:principal:{userId}";

        if (cache.TryGetValue(cacheKey, out HashSet<string>? cached) && cached is not null)
            return cached;

        var dbContext = services.GetRequiredService<AuthDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var permissionResolver = services.GetRequiredService<PermissionResolver>();

        // Same shape as GetMyMenuQueryHandler: user-level grants/denies need the Permissions graph
        // loaded, and PermissionResolver folds the role permissions in on top.
        var user = await dbContext.Users
            .Include(u => u.Permissions)
            .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId);

        // Deactivating a user does not rotate the security stamp, so their Identity cookie stays valid
        // and a page they already have open keeps working — and a polling page like this dashboard
        // keeps sliding that cookie's expiry too. Login.cshtml.cs refuses !IsActive at sign-in; apply
        // the same rule here so deactivation actually closes the door.
        //
        // A deleted or deactivated user is cached as "no permissions" like anyone else — otherwise
        // every polled request would re-run this query for them.
        HashSet<string> permissions = user is null || !user.IsActive
            ? []
            : await permissionResolver.CalculateAsync(user, await userManager.GetRolesAsync(user));

        cache.Set(cacheKey, permissions, CacheLifetime);

        return permissions;
    }
}
