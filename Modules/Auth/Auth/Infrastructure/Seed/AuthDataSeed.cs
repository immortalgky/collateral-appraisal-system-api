using System.Linq;
using Auth.Domain.Companies;
using Auth.Domain.Menu;
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
    private const string IntAdminRoleName = "IntAdmin";
    private const string ExtAdminRoleName = "ExtAdmin";
    private const string RequestMakerRoleName = "RequestMaker";
    private const string RequestCheckerRoleName = "RequestChecker";
    private const string IntAppraisalStaffRoleName = "IntAppraisalStaff";
    private const string IntAppraisalCheckerRoleName = "IntAppraisalChecker";
    private const string IntAppraisalVerifierRoleName = "IntAppraisalVerifier";
    private const string ExtAppraisalStaffRoleName = "ExtAppraisalStaff";
    private const string ExtAppraisalCheckerRoleName = "ExtAppraisalChecker";
    private const string ExtAppraisalVerifierRoleName = "ExtAppraisalVerifier";
    private const string AppraisalCommitteeRoleName = "AppraisalCommittee";

    public async Task SeedAllAsync()
    {
        await SeedPermissionsAsync();
        await SeedMenuItemsAsync();
        await SeedActivityMenuOverridesAsync();
        await SeedAdminRoleAsync();

        string[] appraisalSectionViews =
        [
            "APPRAISAL_360_VIEW", "APPRAISAL_REQUEST_VIEW",
            "APPRAISAL_ADMINISTRATION_VIEW", "APPRAISAL_APPOINTMENT_VIEW",
            "APPRAISAL_PROPERTY_VIEW", "APPRAISAL_BLOCK_CONDO_VIEW",
            "APPRAISAL_BLOCK_VILLAGE_VIEW", "APPRAISAL_PROPERTY_PMA_VIEW",
            "APPRAISAL_DOCUMENTS_VIEW", "APPRAISAL_SUMMARY_VIEW",
        ];
        string[] appraisalSectionEdits =
        [
            "APPRAISAL_REQUEST_EDIT",
            "APPRAISAL_ADMINISTRATION_EDIT", "APPRAISAL_APPOINTMENT_EDIT",
            "APPRAISAL_PROPERTY_EDIT", "APPRAISAL_BLOCK_CONDO_EDIT",
            "APPRAISAL_BLOCK_VILLAGE_EDIT", "APPRAISAL_PROPERTY_PMA_EDIT",
            "APPRAISAL_DOCUMENTS_EDIT", "APPRAISAL_SUMMARY_EDIT",
        ];

        await SeedRoleWithPermissionsAsync(MeetingSecretaryRoleName,
            "Meeting Secretary — creates, schedules, updates, and ends approval meetings.",
            scope: "Bank",
            ["MEETING_MANAGE", "MEETING_SECRETARY"]);
        await SeedRoleWithPermissionsAsync(IntAdminRoleName,
            "Internal Admin — manages workflow assignments, appraisals, meetings, and internal staff.",
            scope: "Bank",
            ["DASHBOARD_VIEW", "REQUEST_VIEW", "TASK_LIST_VIEW", "TASK_APPR_ASSIGNMENT",
             "APPRAISAL_VIEW", "APPRAISAL_REVIEW", "REPORT_VIEW", "REPORT_STATISTICS_VIEW",
             "MEETING_MANAGE", "MEETING_ADMIN", "WORKFLOW_MANAGE", "USER_MANAGE",
             ..appraisalSectionViews]);
        await SeedRoleWithPermissionsAsync(ExtAdminRoleName,
            "External Company Admin — manages external company users and external appraisal assignments.",
            scope: "Company",
            ["DASHBOARD_VIEW", "REQUEST_VIEW", "APPRAISAL_VIEW", "TASK_LIST_VIEW",
             "TASK_EXT_APPR_ASSIGNMENT", "USER_MANAGE",
             ..appraisalSectionViews]);
        await SeedRoleWithPermissionsAsync(RequestMakerRoleName,
            "Request Maker — creates appraisal requests and handles initiation tasks.",
            scope: "Bank",
            ["DASHBOARD_VIEW", "REQUEST_VIEW", "REQUEST_CREATE", "TASK_LIST_VIEW",
             "TASK_APPR_INITIATION_CHECK", "TASK_APPR_INITIATION", "TASK_PROVIDE_ADDITIONAL_DOCS"]);
        await SeedRoleWithPermissionsAsync(RequestCheckerRoleName,
            "Request Checker — reviews and approves incoming appraisal requests.",
            scope: "Bank",
            ["DASHBOARD_VIEW", "REQUEST_VIEW", "TASK_LIST_VIEW", "TASK_APPR_INITIATION_CHECK"]);
        await SeedRoleWithPermissionsAsync(IntAppraisalStaffRoleName,
            "Internal Appraisal Staff — executes internal appraisals and verifies appraisal books.",
            scope: "Bank",
            ["DASHBOARD_VIEW", "APPRAISAL_VIEW", "APPRAISAL_EDIT", "TASK_LIST_VIEW",
             "TASK_APPR_BOOK_VERIFICATION", "TASK_INT_APPR_EXECUTION", "STANDALONE_USE",
             ..appraisalSectionViews, ..appraisalSectionEdits]);
        await SeedRoleWithPermissionsAsync(IntAppraisalCheckerRoleName,
            "Internal Appraisal Checker — checks and validates internal appraisal reports.",
            scope: "Bank",
            ["DASHBOARD_VIEW", "APPRAISAL_VIEW", "APPRAISAL_REVIEW", "TASK_LIST_VIEW",
             "TASK_INT_APPR_CHECK",
             ..appraisalSectionViews]);
        await SeedRoleWithPermissionsAsync(IntAppraisalVerifierRoleName,
            "Internal Appraisal Verifier — final verification of internal appraisal reports.",
            scope: "Bank",
            ["DASHBOARD_VIEW", "APPRAISAL_VIEW", "APPRAISAL_REVIEW", "TASK_LIST_VIEW",
             "TASK_INT_APPR_VERIFICATION", "REPORT_VIEW",
             ..appraisalSectionViews]);
        await SeedRoleWithPermissionsAsync(ExtAppraisalStaffRoleName,
            "External Appraisal Staff — field appraisers from external companies who execute appraisals.",
            scope: "Company",
            ["DASHBOARD_VIEW", "APPRAISAL_VIEW", "APPRAISAL_EDIT", "TASK_LIST_VIEW",
             "TASK_EXT_APPR_ASSIGNMENT", "TASK_EXT_APPR_EXECUTION", "STANDALONE_USE",
             ..appraisalSectionViews, ..appraisalSectionEdits]);
        await SeedRoleWithPermissionsAsync(ExtAppraisalCheckerRoleName,
            "External Appraisal Checker — checks external appraisal reports before verification.",
            scope: "Company",
            ["DASHBOARD_VIEW", "APPRAISAL_VIEW", "APPRAISAL_REVIEW", "TASK_LIST_VIEW",
             "TASK_EXT_APPR_CHECK",
             ..appraisalSectionViews]);
        await SeedRoleWithPermissionsAsync(ExtAppraisalVerifierRoleName,
            "External Appraisal Verifier — final verification of external appraisal reports.",
            scope: "Company",
            ["DASHBOARD_VIEW", "APPRAISAL_VIEW", "APPRAISAL_REVIEW", "TASK_LIST_VIEW",
             "TASK_EXT_APPR_VERIFICATION",
             ..appraisalSectionViews]);
        await SeedRoleWithPermissionsAsync(AppraisalCommitteeRoleName,
            "Appraisal Committee — approves appraisals in committee meetings.",
            scope: "Bank",
            ["DASHBOARD_VIEW", "APPRAISAL_VIEW", "APPRAISAL_REVIEW", "TASK_LIST_VIEW",
             "TASK_PENDING_APPROVAL", "REPORT_VIEW", "REPORT_STATISTICS_VIEW", "MEETING_MANAGE", "COMMITTEE_MEMBER",
             ..appraisalSectionViews]);
        await SeedUsersAsync();
        await SeedClientsAsync();
        await SeedCompaniesAsync();
    }

    private async Task SeedRoleWithPermissionsAsync(
        string roleName, string description, string scope, string[] permissionCodes)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            role = new ApplicationRole
            {
                Name = roleName,
                Description = description,
                Scope = scope
            };

            var createResult = await roleManager.CreateAsync(role);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create {roleName} role: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        var permissionIds = await dbContext.Permissions
            .AsNoTracking()
            .Where(p => permissionCodes.Contains(p.PermissionCode))
            .Select(p => p.Id)
            .ToListAsync();

        var existingLinkedIds = await dbContext.Set<RolePermission>()
            .AsNoTracking()
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        var missingIds = permissionIds.Except(existingLinkedIds).ToList();
        if (missingIds.Count == 0) return;

        var newLinks = missingIds
            .Select(pid => new RolePermission { RoleId = role.Id, PermissionId = pid })
            .ToList();

        await dbContext.Set<RolePermission>().AddRangeAsync(newLinks);
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
            // Per-section permissions for the appraisal layout tabs
            ("APPRAISAL_360_VIEW", "View 360 Summary", "View the 360 Summary tab", "Appraisal"),
            ("APPRAISAL_REQUEST_VIEW", "View Request Information", "View the Request Information tab inside an appraisal", "Appraisal"),
            ("APPRAISAL_REQUEST_EDIT", "Edit Request Information", "Edit the Request Information tab inside an appraisal", "Appraisal"),
            ("APPRAISAL_ADMINISTRATION_VIEW", "View Administration", "View the Administration tab", "Appraisal"),
            ("APPRAISAL_ADMINISTRATION_EDIT", "Edit Administration", "Edit the Administration tab", "Appraisal"),
            ("APPRAISAL_APPOINTMENT_VIEW", "View Appointment & Fee", "View the Appointment & Fee tab", "Appraisal"),
            ("APPRAISAL_APPOINTMENT_EDIT", "Edit Appointment & Fee", "Edit the Appointment & Fee tab", "Appraisal"),
            ("APPRAISAL_PROPERTY_VIEW", "View Property Information", "View the Property Information tab", "Appraisal"),
            ("APPRAISAL_PROPERTY_EDIT", "Edit Property Information", "Edit the Property Information tab", "Appraisal"),
            ("APPRAISAL_BLOCK_CONDO_VIEW", "View Property Information (Condo)", "View the Condo block tab", "Appraisal"),
            ("APPRAISAL_BLOCK_CONDO_EDIT", "Edit Property Information (Condo)", "Edit the Condo block tab", "Appraisal"),
            ("APPRAISAL_BLOCK_VILLAGE_VIEW", "View Property Information (Village)", "View the Village block tab", "Appraisal"),
            ("APPRAISAL_BLOCK_VILLAGE_EDIT", "Edit Property Information (Village)", "Edit the Village block tab", "Appraisal"),
            ("APPRAISAL_PROPERTY_PMA_VIEW", "View Property Information (PMA)", "View the PMA tab", "Appraisal"),
            ("APPRAISAL_PROPERTY_PMA_EDIT", "Edit Property Information (PMA)", "Edit the PMA tab", "Appraisal"),
            ("APPRAISAL_DOCUMENTS_VIEW", "View Document Checklist", "View the Document Checklist tab", "Appraisal"),
            ("APPRAISAL_DOCUMENTS_EDIT", "Edit Document Checklist", "Edit the Document Checklist tab", "Appraisal"),
            ("APPRAISAL_SUMMARY_VIEW", "View Summary & Decision", "View the Summary & Decision tab", "Appraisal"),
            ("APPRAISAL_SUMMARY_EDIT", "Edit Summary & Decision", "Edit the Summary & Decision tab", "Appraisal"),
            ("USER_MANAGE", "Manage Users", "Create, update, and deactivate user accounts", "Auth"),
            ("ROLE_MANAGE", "Manage Roles", "Create, update, and delete roles and role permissions", "Auth"),
            ("PERMISSION_MANAGE", "Manage Permissions", "Create, update, and delete permissions", "Auth"),
            ("GROUP_MANAGE", "Manage Groups", "Create, update, and delete groups and group members", "Auth"),
            ("USER_CHANGE_PASSWORD", "Change Any User Password", "Allow changing password for any local user", "Auth"),
            ("USER_RESET_PASSWORD", "Reset User Password", "Allow resetting password for any local user without current password", "Auth"),
            ("MEETING_MANAGE", "Manage Meetings", "Create, schedule, update, cancel, and end approval meetings", "Workflow"),
            ("MEETING_ADMIN", "Meeting Admin", "Create, schedule, cut-off, cancel, and end approval meetings", "Workflow"),
            ("MEETING_SECRETARY", "Meeting Secretary", "After meeting ends, release each appraisal to approvers or route back to appraisal team", "Workflow"),
            ("COMMITTEE_MEMBER", "Committee Member", "Participate in committee meetings — view meeting details, agenda, and appraisal items", "Workflow"),
            // --- DB-driven navigation menu feature ---
            ("MENU_MANAGE", "Manage Menus", "Create, update, and delete navigation menu items", "Auth"),
            ("REQUEST_VIEW", "View Requests", "View appraisal requests", "Request"),
            ("REQUEST_CREATE", "Create Requests", "Create new appraisal requests", "Request"),
            ("APPRAISAL_REVIEW", "Review Appraisals", "Review appraisals pending checker/verifier action", "Appraisal"),
            ("REPORT_VIEW", "View Reports", "View completed reports", "Common"),
            ("REPORT_STATISTICS_VIEW", "View Report Statistics", "View report statistics dashboards", "Common"),
            ("STANDALONE_USE", "Use Standalone Tools", "Access standalone appraisal tools", "Common"),
            ("PARAMETER_MANAGE", "Manage Parameters", "Manage system parameters", "Common"),
            ("WORKFLOW_MANAGE", "Manage Workflows", "Manage workflow definitions", "Workflow"),
            ("TEMPLATE_MANAGE", "Manage Templates", "Manage MC/comparative templates", "Appraisal"),
            // Per-activity task gating (Module = Workflow)
            ("TASK_APPR_INITIATION_CHECK", "Task: Appraisal Initiation Check", "Access appraisal initiation check tasks", "Workflow"),
            ("TASK_APPR_INITIATION", "Task: Appraisal Initiation", "Access appraisal initiation tasks", "Workflow"),
            ("TASK_APPR_ASSIGNMENT", "Task: Appraisal Assignment", "Access internal appraisal assignment tasks", "Workflow"),
            ("TASK_EXT_APPR_ASSIGNMENT", "Task: External Appraisal Assignment", "Access external appraisal assignment tasks", "Workflow"),
            ("TASK_EXT_APPR_EXECUTION", "Task: External Appraisal Execution", "Access external appraisal execution tasks", "Workflow"),
            ("TASK_EXT_APPR_CHECK", "Task: External Appraisal Check", "Access external appraisal check tasks", "Workflow"),
            ("TASK_EXT_APPR_VERIFICATION", "Task: External Appraisal Verification", "Access external appraisal verification tasks", "Workflow"),
            ("TASK_APPR_BOOK_VERIFICATION", "Task: Appraisal Book Verification", "Access appraisal book verification tasks", "Workflow"),
            ("TASK_INT_APPR_EXECUTION", "Task: Internal Appraisal Execution", "Access internal appraisal execution tasks", "Workflow"),
            ("TASK_INT_APPR_CHECK", "Task: Internal Appraisal Check", "Access internal appraisal check tasks", "Workflow"),
            ("TASK_INT_APPR_VERIFICATION", "Task: Internal Appraisal Verification", "Access internal appraisal verification tasks", "Workflow"),
            ("TASK_PENDING_APPROVAL", "Task: Pending Approval", "Access pending approval tasks", "Workflow"),
            ("TASK_PROVIDE_ADDITIONAL_DOCS", "Task: Provide Additional Documents", "Access document followup tasks raised by checkers", "Workflow"),
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

    private async Task SeedMenuItemsAsync()
    {
        var mainRoots = MenuSeedData.GetMainMenuSeed();
        var appraisalRoots = MenuSeedData.GetAppraisalMenuSeed();

        var existingByKey = await dbContext.MenuItems
            .Include(m => m.Translations)
            .ToDictionaryAsync(m => m.ItemKey);

        await UpsertTreeAsync(mainRoots, MenuScope.Main, parentId: null, existingByKey);
        await UpsertTreeAsync(appraisalRoots, MenuScope.Appraisal, parentId: null, existingByKey);

        await dbContext.SaveChangesAsync();
    }

    private async Task UpsertTreeAsync(
        List<MenuSeedData.MenuSeedNode> nodes,
        MenuScope scope,
        Guid? parentId,
        Dictionary<string, MenuItem> existingByKey)
    {
        var sortOrder = 10;
        foreach (var node in nodes)
        {
            MenuItem item;

            // INSERT-ONLY: if the ItemKey already exists, do NOT overwrite admin edits.
            // The seeder runs on every boot via UseMigration<AuthDbContext>, so any Update/
            // Reparent/ReplaceTranslations here would roll back label/icon/permission changes
            // made via /admin/menus. For intentional shape changes to seeded items, write a
            // one-off migration script rather than touching the seeder.
            if (existingByKey.TryGetValue(node.ItemKey, out var existing))
            {
                item = existing;
            }
            else
            {
                item = MenuItem.Create(
                    node.ItemKey,
                    scope,
                    parentId,
                    node.Path,
                    MenuIcon.Create(node.IconName, node.IconStyle),
                    node.IconColor,
                    sortOrder,
                    node.ViewPermissionCode,
                    node.EditPermissionCode,
                    BuildTranslations(node.LabelEn),
                    isSystem: true);
                dbContext.MenuItems.Add(item);
                existingByKey[node.ItemKey] = item;
            }

            sortOrder += 10;

            if (node.Children is { Count: > 0 })
                await UpsertTreeAsync(node.Children, scope, item.Id, existingByKey);
        }
    }

    private static List<MenuItemTranslation> BuildTranslations(string labelEn) => new()
    {
        MenuItemTranslation.Create("en", labelEn),
        MenuItemTranslation.Create("th", labelEn),
        MenuItemTranslation.Create("zh", labelEn),
    };

    private async Task SeedActivityMenuOverridesAsync()
    {
        var seed = ActivityMenuOverrideSeedData.GetSeed();
        if (seed.Count == 0) return;

        var menuItemIdsByKey = await dbContext.MenuItems
            .Where(m => m.Scope == MenuScope.Appraisal)
            .ToDictionaryAsync(m => m.ItemKey, m => m.Id);

        var existing = await dbContext.ActivityMenuOverrides
            .ToDictionaryAsync(o => (o.ActivityId, o.MenuItemId));

        var added = false;
        foreach (var entry in seed)
        {
            if (!menuItemIdsByKey.TryGetValue(entry.MenuItemKey, out var menuItemId))
                continue; // menu item not seeded yet — skip gracefully

            if (existing.ContainsKey((entry.ActivityId, menuItemId)))
                continue; // INSERT-ONLY, like MenuSeedData — admin edits win.

            var row = ActivityMenuOverride.Create(entry.ActivityId, menuItemId, entry.IsVisible, entry.CanEdit);
            dbContext.ActivityMenuOverrides.Add(row);
            added = true;
        }

        if (added)
            await dbContext.SaveChangesAsync();
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