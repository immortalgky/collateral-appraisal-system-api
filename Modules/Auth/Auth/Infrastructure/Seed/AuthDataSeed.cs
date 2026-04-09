using System.Linq;
using Auth.Domain.Companies;
using Auth.Infrastructure.Repository;
using Microsoft.Extensions.Configuration;

namespace Auth.Infrastructure.Seed;

public class AuthDataSeed(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    AuthDbContext dbContext,
    IOpenIddictApplicationManager manager,
    ICompanyRepository companyRepository,
    IPermissionRepository permissionRepository,
    IConfiguration configuration)
    : IDataSeeder<AuthDbContext>
{
    private const string AdminRoleName = "Admin";
    private const string MeetingSecretaryRoleName = "MeetingSecretary";

    public async Task SeedAllAsync()
    {
        await SeedPermissionsAsync();
        await SeedAdminRoleAsync();
        await SeedMeetingSecretaryRoleAsync();
        await SeedUsersAsync();
        await SeedClientsAsync();
        await SeedCompaniesAsync();
    }

    private async Task SeedMeetingSecretaryRoleAsync()
    {
        var role = await roleManager.FindByNameAsync(MeetingSecretaryRoleName);
        if (role is null)
        {
            role = new ApplicationRole
            {
                Name = MeetingSecretaryRoleName,
                Description = "Meeting Secretary — creates, schedules, updates, and ends approval meetings."
            };

            var createResult = await roleManager.CreateAsync(role);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create MeetingSecretary role: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        var meetingPermission = await dbContext.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PermissionCode == "MEETING_MANAGE");
        if (meetingPermission is null)
            return;

        var alreadyLinked = await dbContext.Set<RolePermission>()
            .AsNoTracking()
            .AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == meetingPermission.Id);

        if (alreadyLinked)
            return;

        await dbContext.Set<RolePermission>().AddAsync(new RolePermission
        {
            RoleId = role.Id,
            PermissionId = meetingPermission.Id
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedUsersAsync()
    {
        // Create a default admin user only if configured
        var adminConfig = configuration.GetSection("SeedData:AdminUser");
        var adminUsername = adminConfig["Username"];
        var adminPassword = adminConfig["Password"];

        if (!string.IsNullOrEmpty(adminUsername) && !string.IsNullOrEmpty(adminPassword))
        {
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

            var adminUser = await userManager.FindByNameAsync(adminUsername);
            if (adminUser is not null && !await userManager.IsInRoleAsync(adminUser, AdminRoleName))
                await userManager.AddToRoleAsync(adminUser, AdminRoleName);
        }

        // Seed additional test users
        var testUsers =
            new List<(string Username, string Email, string FirstName, string LastName, string? Position, string?
                Department, string AvatarColor)>
            {
                ("john.doe", "john.doe@example.com", "John", "Doe", "Senior Appraiser", "Appraisal", "0891b2"),
                ("jane.smith", "jane.smith@example.com", "Jane", "Smith", "Branch Manager", "Operations", "7c3aed"),
                ("m.wilson", "m.wilson@example.com", "Mike", "Wilson", "Loan Officer", "Lending", "059669"),
                ("s.johnson", "s.johnson@example.com", "Sarah", "Johnson", "Quality Analyst",
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

    private async Task SeedCompaniesAsync()
    {
        var seedCompanies = new List<(string Name, string? TaxId, string? Province, List<string> LoanTypes)>
        {
            ("Thai Appraisal Co., Ltd.", "0105550001234", "Bangkok", ["Retail", "IBG"]),
            ("Siam Valuation Group", "0105550005678", "Bangkok", ["Retail"]),
            ("Bangkok Property Appraisers", "0105550009012", "Bangkok", ["IBG"]),
            ("Eastern Appraisal Services", "0205560003456", "Chonburi", ["Retail", "IBG"]),
            ("Northern Valuation Partners", "0505570007890", "Chiang Mai", ["Retail"]),
            ("Southern Property Consultants", "0905580001234", "Songkhla", ["IBG"])
        };

        foreach (var (name, taxId, province, loanTypes) in seedCompanies)
        {
            var existing = await companyRepository.GetByNameAsync(name);
            if (existing is null)
            {
                var company = Company.Create(
                    name,
                    taxId: taxId,
                    province: province,
                    loanTypes: loanTypes);
                await companyRepository.AddAsync(company);
            }
        }

        await companyRepository.SaveChangesAsync();
    }

    private async Task SeedPermissionsAsync()
    {
        var seedPermissions = new List<(string Code, string DisplayName, string Description, string Module)>
        {
            ("DASHBOARD_VIEW", "View Dashboard", "Access the main dashboard", "Common"),
            ("TASK_LIST_VIEW", "View Task List", "View and manage assigned tasks", "Workflow"),
            ("APPRAISAL_VIEW", "View Appraisal", "View appraisal requests and details", "Appraisal"),
            ("APPRAISAL_CREATE", "Create Appraisal", "Create new appraisal requests", "Appraisal"),
            ("APPRAISAL_EDIT", "Edit Appraisal", "Edit existing appraisal requests", "Appraisal"),
            ("APPRAISAL_DELETE", "Delete Appraisal", "Delete appraisal requests", "Appraisal"),
            ("USER_MANAGE", "Manage Users", "Create, update, and deactivate user accounts", "Auth"),
            ("ROLE_MANAGE", "Manage Roles", "Create, update, and delete roles and role permissions", "Auth"),
            ("PERMISSION_MANAGE", "Manage Permissions", "Create, update, and delete permissions", "Auth"),
            ("GROUP_MANAGE", "Manage Groups", "Create, update, and delete groups and group members", "Auth"),
            ("USER_CHANGE_PASSWORD", "Change Any User Password", "Allow changing password for any local user", "Auth"),
            ("USER_RESET_PASSWORD", "Reset User Password", "Allow resetting password for any local user without current password", "Auth"),
            ("MEETING_MANAGE", "Manage Meetings", "Create, schedule, update, cancel, and end approval meetings", "Workflow"),
        };

        foreach (var (code, displayName, description, module) in seedPermissions)
        {
            var exists = await permissionRepository.CodeExistsAsync(code);
            if (!exists)
            {
                var permission = Permission.Create(code, displayName, description, module);
                await permissionRepository.AddAsync(permission);
            }
        }

        await permissionRepository.SaveChangesAsync();
    }

    private async Task SeedAdminRoleAsync()
    {
        var adminRole = await roleManager.FindByNameAsync(AdminRoleName);
        if (adminRole is null)
        {
            adminRole = new ApplicationRole
            {
                Name = AdminRoleName,
                Description = "Full system access — auto-granted every permission by the seeder."
            };

            var createResult = await roleManager.CreateAsync(adminRole);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create Admin role: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        var allPermissionIds = await dbContext.Permissions
            .AsNoTracking()
            .Select(p => p.Id)
            .ToListAsync();

        var existingLinkedIds = await dbContext.Set<RolePermission>()
            .AsNoTracking()
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        var missingIds = allPermissionIds.Except(existingLinkedIds).ToList();
        if (missingIds.Count == 0) return;

        var newLinks = missingIds
            .Select(pid => new RolePermission { RoleId = adminRole.Id, PermissionId = pid })
            .ToList();

        await dbContext.Set<RolePermission>().AddRangeAsync(newLinks);
        await dbContext.SaveChangesAsync();
    }
}