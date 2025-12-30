using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using OAuth2OpenId.Data.Repository;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Server.AspNetCore;

namespace OAuth2OpenId.Services;

public class TokenService(
    OpenIddictDbContext openIddictDbContext,
    UserManager<ApplicationUser> userManager,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager,
    IRoleRepository roleRepository
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
        { OpenIddictConstants.Claims.PhoneNumberVerified, OpenIddictConstants.Scopes.Phone },
    };

    public async Task<ClaimsPrincipal> CreateAuthCodeFlowAccessTokenPrincipal(
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
        var user =
            await openIddictDbContext
                .Users.Include(user => user.Permissions)
                .ThenInclude(userPermission => userPermission.Permission)
                .FirstOrDefaultAsync(user => user.Id == userIdGuid)
            ?? throw new InvalidOperationException("Cannot find user associated with the token.");

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.AddClaim(OpenIddictConstants.Claims.Subject, userId);
        identity.AddClaim(OpenIddictConstants.Claims.Name, username);
        identity.SetClaims("permissions", await GetUserPermissions(user));

        identity.SetClaims("roles", [.. await userManager.GetRolesAsync(user)]);

        identity.SetDestinations(GetDestinations);

        var claimsPrincipal = new ClaimsPrincipal(identity);
        claimsPrincipal.SetScopes(request.GetScopes());

        return claimsPrincipal;
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
        if (ClaimScopeMapping.ContainsKey(claim.Type))
        {
            return claim.Subject!.HasScope(ClaimScopeMapping[claim.Type])
                ? [OpenIddictConstants.Destinations.IdentityToken]
                : [];
        }
        else
        {
            return [OpenIddictConstants.Destinations.AccessToken];
        }
    }

    internal async Task<ImmutableArray<string>> GetUserPermissions(ApplicationUser user)
    {
        var roleNames = await userManager.GetRolesAsync(user);
        return await CalcUserPermissions(user.Permissions, roleNames, roleRepository);
    }
    
    internal static async Task<ImmutableArray<string>> CalcUserPermissions(IList<UserPermission> userPermissions, IList<string> roleNames, IRoleRepository roleRepository)
    {
        var permissions = new HashSet<string>();
        var ungrantedPermissions = new HashSet<string>();

        foreach (var permission in userPermissions)
        {
            if (permission.IsGranted)
            {
                permissions.Add(permission.Permission.PermissionCode);
            }
            else
            {
                ungrantedPermissions.Add(permission.Permission.PermissionCode);
            }
        }

        foreach (var roleName in roleNames)
        {
            var role = await roleRepository.GetRoleByName(roleName)
                ?? throw new InvalidOperationException($"Cannot find role named {roleName}");

            foreach (
                var allowedRolePermission in role.Permissions.Where(rolePermission =>
                    !ungrantedPermissions.Contains(rolePermission.Permission.PermissionCode)
                )
            )
            {
                permissions.Add(allowedRolePermission.Permission.PermissionCode);
            }
        }

        return [.. permissions];

    }
}
