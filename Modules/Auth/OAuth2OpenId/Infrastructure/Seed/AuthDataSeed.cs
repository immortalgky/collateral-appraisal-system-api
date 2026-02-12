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
        await SeedUsersAsync();
        await SeedClientsAsync();
    }

    private async Task SeedUsersAsync()
    {
        // Create a default admin user only if configured
        var adminConfig = configuration.GetSection("SeedData:AdminUser");
        var adminUsername = adminConfig["Username"];
        var adminPassword = adminConfig["Password"];

        if (!string.IsNullOrEmpty(adminUsername) && !string.IsNullOrEmpty(adminPassword))
            if (await userManager.FindByNameAsync(adminUsername) is null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminUsername,
                    Email = "admin@example.com",
                    FirstName = "System",
                    LastName = "Administrator",
                    Position = "System Administrator",
                    Department = "IT",
                    AvatarUrl =
                        "https://ui-avatars.com/api/?name=System+Administrator&background=4f46e5&color=fff&size=128"
                };
                var result = await userManager.CreateAsync(admin, adminPassword);

                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

        // Seed additional test users
        var testUsers =
            new List<(string Username, string Email, string FirstName, string LastName, string? Position, string?
                Department, string AvatarColor)>
            {
                ("john.doe", "john.doe@example.com", "John", "Doe", "Senior Appraiser", "Appraisal", "0891b2"),
                ("jane.smith", "jane.smith@example.com", "Jane", "Smith", "Branch Manager", "Operations", "7c3aed"),
                ("mike.wilson", "mike.wilson@example.com", "Mike", "Wilson", "Loan Officer", "Lending", "059669"),
                ("sarah.johnson", "sarah.johnson@example.com", "Sarah", "Johnson", "Quality Analyst",
                    "Quality Assurance", "dc2626"),
                ("thitipornw", "thitipornw@silverlakeaxis.com", "Thitiporn", "W", "Quality Analyst",
                    "Quality Assurance", "dc2626"),
                ("shen.low", "shennee.low@silverlakeaxis.com", "Shen", "Low", "Quality Analyst", "Quality Assurance",
                    "dc2626"),
                ("ckchong", "ckchong@silverlakeaxis.com", "CK", "Chong", "Quality Analyst", "Quality Assurance",
                    "dc2626")
            };

        foreach (var (username, email, firstName, lastName, position, department, avatarColor) in testUsers)
            if (await userManager.FindByNameAsync(username) is null)
            {
                var user = new ApplicationUser
                {
                    UserName = username,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    Position = position,
                    Department = department,
                    AvatarUrl =
                        $"https://ui-avatars.com/api/?name={firstName}+{lastName}&background={avatarColor}&color=fff&size=128"
                };
                await userManager.CreateAsync(user, "P@ssw0rd!");
            }
    }

    private async Task SeedClientsAsync()
    {
        if (await manager.FindByClientIdAsync("spa") is null)
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "spa",
                //ClientSecret = "P@ssw0rd",
                DisplayName = "SPA",
                ClientType = OpenIddictConstants.ClientTypes.Public,
                PostLogoutRedirectUris = { new Uri("https://localhost:3000/") },
                RedirectUris =
                {
                    new Uri("https://localhost:7111/callback"),
                    new Uri("https://localhost:3000/callback")
                },
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.EndSession,
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

        if (await manager.FindByClientIdAsync("los") is null)
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "los",
                ClientSecret = "P@ssw0rd",
                DisplayName = "LOS",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                PostLogoutRedirectUris = { new Uri("https://localhost:3000") },
                RedirectUris =
                {
                    new Uri("https://localhost:7111/callback"),
                    new Uri("https://localhost:3000/callback")
                },
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.EndSession,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.Password,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
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