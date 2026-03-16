using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Auth.Infrastructure.Seed;

public class AuthDataSeed(
    UserManager<ApplicationUser> userManager,
    IOpenIddictApplicationManager manager,
    IConfiguration configuration)
    : IDataSeeder<AuthDbContext>
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
                    "dc2626"),
                // LH Bank tester users
                ("P2560", "p2560@lhbank.co.th", "Kosol", "Kavayavong", "BU", null, "0891b2"),
                ("P0236", "p0236@lhbank.co.th", "Chalearmporn", "Suwandee", "BU", null, "0891b2"),
                ("P0108", "p0108@lhbank.co.th", "Kodchaporn", "Prateepnumchai", "BU", null, "0891b2"),
                ("P2248", "p2248@lhbank.co.th", "Thammaporn", "Somsook", "BU", null, "0891b2"),
                ("P4252", "p4252@lhbank.co.th", "Rujiphong", "Boonnithiworakul", "BU", null, "0891b2"),
                ("P1977", "p1977@lhbank.co.th", "Ekkasin", "Kaewchat", "BU", null, "0891b2"),
                ("P1990", "p1990@lhbank.co.th", "Aeksiri", "Su-ang-ka", "BU", null, "0891b2"),
                ("P1906", "p1906@lhbank.co.th", "Pricha", "Muongna", "BU", null, "0891b2"),
                ("P0994", "p0994@lhbank.co.th", "Weera", "Saechua", "BU", null, "0891b2"),
                ("P4232", "p4232@lhbank.co.th", "Sattar", "Samantarath", "BU", null, "0891b2"),
                ("P4859", "p4859@lhbank.co.th", "Chadchawal", "Teamsri", "BU", null, "0891b2"),
                ("P6354", "p6354@lhbank.co.th", "Thanwa", "Chamnanrabeabkit", "BU", null, "0891b2"),
                ("P2405", "p2405@lhbank.co.th", "Prachaya", "Jiamsuwan", "BU", null, "0891b2"),
                ("P5863", "p5863@lhbank.co.th", "Phuvanai", "Pienchuaisuk", "BU", null, "0891b2"),
                ("P4418", "p4418@lhbank.co.th", "Bordin", "Treerattaweechai", "BU", null, "0891b2"),
                ("P1163", "p1163@lhbank.co.th", "Saowalak", "Sukonta", "BU", null, "0891b2"),
                ("P6317", "p6317@lhbank.co.th", "Montree", "Somboonkitpattana", "BU", null, "0891b2"),
                ("P6762", "p6762@lhbank.co.th", "Jintita", "Oupakhot", "BPI", null, "7c3aed"),
                ("P6458", "p6458@lhbank.co.th", "Piyaphon", "Polharn", "BPI", null, "7c3aed"),
                ("P4452", "p4452@lhbank.co.th", "Wasin", "Auayingsak", "Dev", null, "059669"),
                ("P5229", "p5229@lhbank.co.th", "Nunpanita", "Thanasombatmankhong", "Dev", null, "059669"),
                ("P5756", "p5756@lhbank.co.th", "Danaikorn", "Phoosomsri", "QA", null, "dc2626"),
                ("TADI025", "tadi025@lhbank.co.th", "Danai", "Phiromwong", "QA", null, "dc2626"),
                ("P5201", "p5201@lhbank.co.th", "Nipon", "Pornperdprai", "PMO", null, "ea580c")
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

        // CLS (Corporate Loan Origination System) Integration Client
        // Uses client credentials flow for machine-to-machine communication
        if (await manager.FindByClientIdAsync("cls") is null)
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "cls",
                ClientSecret = "CLS_SecretKey_2024!", // TODO: Use configuration for production
                DisplayName = "CLS Integration",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                Permissions =
                {
                    // Endpoints
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    // Grant types - client credentials only for M2M
                    OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                    // CLS-specific scopes
                    OpenIddictConstants.Permissions.Prefixes.Scope + "appraisal.read",
                    OpenIddictConstants.Permissions.Prefixes.Scope + "request.write",
                    OpenIddictConstants.Permissions.Prefixes.Scope + "document.read",
                    OpenIddictConstants.Permissions.Prefixes.Scope + "document.write"
                }
            });
    }
}