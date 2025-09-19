using System.Linq;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Server.AspNetCore;

namespace OAuth2OpenId.Services;

public class TokenService(
    OpenIddictDbContext openIddictDbContext,
    UserManager<ApplicationUser> userManager,
    IOpenIddictApplicationManager manager
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
        var userId = principal.FindFirstValue(OpenIddictConstants.Claims.Subject);
        var username = principal.FindFirstValue(OpenIddictConstants.Claims.Name);

        var userIdGuid = Guid.Parse(userId); // Can throws exception
        var user = await openIddictDbContext
            .Users.Include(user => user.Permissions)
            .FirstOrDefaultAsync(user => user.Id == userIdGuid);

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.AddClaim(OpenIddictConstants.Claims.Subject, userId);
        identity.AddClaim(OpenIddictConstants.Claims.Name, username);
        identity.SetClaims(
            "permissions",
            [.. user.Permissions.Select(permission => permission.PermissionName)]
        );

        if (identity.HasScope(OpenIddictConstants.Scopes.Roles))
        {
            identity.SetClaims("roles", [.. await userManager.GetRolesAsync(user)]);
        }

        identity.SetDestinations(GetDestinations);

        var claimsPrincipal = new ClaimsPrincipal(identity);
        claimsPrincipal.SetScopes(request.GetScopes());

        return claimsPrincipal;
    }

    public async Task<ClaimsPrincipal> CreateClientCredFlowAccessTokenPrincipal(
        OpenIddictRequest request
    )
    {
        var application =
            (OpenIddictEntityFrameworkCoreApplication)
                await manager.FindByClientIdAsync(request.ClientId) ?? throw new Exception();

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role
        );

        identity.SetClaim(
            OpenIddictConstants.Claims.Subject,
            await manager.GetClientIdAsync(application)
        );
        identity.SetClaim(
            OpenIddictConstants.Claims.Name,
            await manager.GetDisplayNameAsync(application)
        );
        identity.SetClaim("aud", "resource_server");
        identity.SetClaims("permissions", await manager.GetPermissionsAsync(application));

        identity.SetDestinations(GetDestinations);

        var claimsPrincipal = new ClaimsPrincipal(identity);
        claimsPrincipal.SetScopes(request.GetScopes());
        return claimsPrincipal;
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        if (ClaimScopeMapping.ContainsKey(claim.Type))
        {
            return claim.Subject.HasScope(ClaimScopeMapping[claim.Type])
                ? [OpenIddictConstants.Destinations.IdentityToken]
                : [];
        }
        else
        {
            return [OpenIddictConstants.Destinations.AccessToken];
        }
    }
}
