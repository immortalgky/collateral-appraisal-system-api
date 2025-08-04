using System.Linq;
using Microsoft.Extensions.Configuration;

namespace OAuth2OpenId.Data.Seed;

public class AuthDataSeed(
    UserManager<ApplicationUser> userManager,
    IOpenIddictApplicationManager manager,
    IConfiguration configuration)
    : IDataSeeder<OpenIddictDbContext>
{
    public async Task SeedAllAsync()
    {
        // Create a default admin user only if configured
        var adminConfig = configuration.GetSection("SeedData:AdminUser");
        var adminUsername = adminConfig["Username"];
        var adminPassword = adminConfig["Password"];

        if (!string.IsNullOrEmpty(adminUsername) && !string.IsNullOrEmpty(adminPassword))
        {
            if (await userManager.FindByNameAsync(adminUsername) is null)
            {
                var admin = new ApplicationUser { UserName = adminUsername };
                var result = await userManager.CreateAsync(admin, adminPassword);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }

        if (await manager.FindByClientIdAsync("spa") is null)
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "spa",
                //ClientSecret = "P@ssw0rd",
                DisplayName = "SPA",
                ClientType = OpenIddictConstants.ClientTypes.Public,
                PostLogoutRedirectUris = { new Uri("https://localhost:7111/") },
                RedirectUris =
                {
                    new Uri("https://localhost:7111/callback"),
                    new Uri("https://localhost:3000/callback")
                },
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.Password,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddictConstants.Permissions.Scopes.Email,
                    OpenIddictConstants.Permissions.Scopes.Profile,
                    OpenIddictConstants.Permissions.Scopes.Roles
                },
                Requirements =
                {
                    OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange // ✅ PKCE required
                }
            });
    }
}