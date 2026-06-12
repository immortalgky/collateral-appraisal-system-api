using Auth.Application.Configurations;
using Auth.Domain.Companies;
using Auth.Infrastructure.Configuration;
using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Repository;
using Auth.Infrastructure.Seed;
using Auth.Application.Services;
using Auth.Contracts.Users;
using Auth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;
using Shared.Identity;
using Shared.Security;

namespace Auth;

public static class AuthModule
{
    public static IServiceCollection AddAuthModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Razor Pages for login UI
        services.AddRazorPages().AddApplicationPart(typeof(Login).Assembly);

        // Anti-forgery protection
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__RequestVerificationToken";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        // Certificate provider
        services.AddSingleton<ICertificateProvider, CertificateProvider>();

        // DbContext
        services.AddDbContext<AuthDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(AuthDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "auth");
            });
            options.UseOpenIddict();
        });

        // ASP.NET Identity
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // Complexity rules are enforced by DbPasswordValidator from the DB-maintained policy
                // (so admin edits apply without a restart). Relax the built-in checks so they don't
                // double-enforce a stale, hardcoded rule set.
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequiredLength = 1;
                options.Password.RequiredUniqueChars = 0;

                // Lockout is fully DB-maintained: these are only first-boot fallbacks (used until the
                // policy row exists). UseAuthModule's ApplyLockoutPolicy overrides all of them from the
                // DB policy at startup (Identity reads lockout options as a snapshot).
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromDays(365 * 200);
                options.Lockout.AllowedForNewUsers = true;

                // Reject duplicate emails. Without this the default UserValidator skips the email
                // uniqueness check, so create/update would silently allow two accounts to share an
                // email (there is no unique index on NormalizedEmail either).
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddPasswordValidator<DbPasswordValidator>()
            .AddDefaultTokenProviders();

        // Point the Identity application cookie at pages that actually exist. The default
        // AccessDeniedPath (/Account/AccessDenied) had no page, so any cookie-auth forbid
        // (e.g. a signed-in non-Admin hitting /hangfire) returned a bare 404 instead of a
        // clear "access denied" page. LoginPath is set explicitly for the same reason.
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });

        // DB-maintained password policy: cached reader + history recorder. Lockout settings are
        // applied imperatively after migration/seeding in UseAuthModule (NOT via IConfigureOptions —
        // reading the DB during options-configuration at container startup deadlocks).
        services.AddScoped<IPasswordPolicyProvider, PasswordPolicyProvider>();
        services.AddScoped<IPasswordHistoryRecorder, PasswordHistoryRecorder>();

        // Unit of Work for the Auth module — enables ITransactionalCommand<IAuthUnitOfWork> so
        // multi-step writes (e.g. user creation + group/team links) are atomic.
        services.AddScoped<IAuthUnitOfWork, AuthUnitOfWork>();

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        // OpenIddict
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<AuthDbContext>();
            })
            .AddServer(options =>
            {
                var appBaseUrl = configuration["AppBaseUrl"];
                if (!string.IsNullOrEmpty(appBaseUrl))
                    options.SetIssuer(new Uri(appBaseUrl));

                options.SetTokenEndpointUris("/connect/token");
                options.SetAuthorizationEndpointUris("/connect/authorize");
                options.SetEndSessionEndpointUris("/connect/logout");

                options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
                options.AllowClientCredentialsFlow();
                options.AllowPasswordFlow();
                options.AllowRefreshTokenFlow();

                options.UseReferenceRefreshTokens();

                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(7));
                options.SetIdentityTokenLifetime(TimeSpan.FromMinutes(15));

                if (environment == "Development") options.AcceptAnonymousClients();

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.OfflineAccess,
                    "appraisal.read", "request.write", "document.read", "document.write", "integration");

                if (environment == "Development")
                {
                    options.AddDevelopmentEncryptionCertificate();
                    options.AddDevelopmentSigningCertificate();
                    options.DisableAccessTokenEncryption();
                }
                else
                {
                    var signingCertConfig = configuration.GetSection("OAuth2:SigningCertificate");
                    var encryptionCertConfig = configuration.GetSection("OAuth2:EncryptionCertificate");

                    if (!signingCertConfig.Exists() || !encryptionCertConfig.Exists())
                        throw new InvalidOperationException(
                            "Production requires OAuth2:SigningCertificate and OAuth2:EncryptionCertificate configuration. " +
                            "Configure both sections (Source = 'store' | 'file') in appsettings.{Environment}.json.");

                    // AddOpenIddict configures at service-registration time, so DI isn't available yet.
                    // Instantiate the provider directly — it only reads IConfiguration.
                    using var certLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
                    var certProvider = new CertificateProvider(
                        configuration,
                        certLoggerFactory.CreateLogger<CertificateProvider>());

                    options.AddSigningCertificate(certProvider.GetSigningCertificate());
                    options.AddEncryptionCertificate(certProvider.GetEncryptionCertificate());
                    // Access-token encryption stays ENABLED in production — both servers must
                    // share the same encryption cert (handled by deploying the same PFX to both).
                }

                options.UseAspNetCore()
                    .EnableTokenEndpointPassthrough()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        // Authentication scheme
        // Dev-auth bypass is allowed in all non-production environments
        // (Development, SIT, UAT). An unset/empty env var is treated as Production
        // so a misconfigured server fails closed.
        var allowDevBypass =
            !string.IsNullOrEmpty(environment) &&
            !environment.Equals("Production", StringComparison.OrdinalIgnoreCase);
        if (allowDevBypass)
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "DevOrOpenIddict";
                    options.DefaultChallengeScheme =
                        OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(
                    DevAuthenticationHandler.SchemeName, _ => { })
                .AddPolicyScheme("DevOrOpenIddict", "Dev or OpenIddict", options =>
                {
                    options.ForwardDefaultSelector = context =>
                    {
                        if (context.Request.Headers.ContainsKey(DevAuthenticationHandler.DevHeaderName))
                            return DevAuthenticationHandler.SchemeName;
                        return OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                    };
                });
        }
        else
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme =
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            });
        }

        // Authorization policies
        services.AddAuthorizationBuilder().AddPolicies(allowDevBypass);

        // Data seeding
        services.AddScoped<IDataSeeder<AuthDbContext>, AuthDataSeed>();

        // LDAP
        services.Configure<LdapConfiguration>(configuration.GetSection(LdapConfiguration.SectionName));
        services.AddScoped<ILdapAuthenticationService, LdapAuthenticationService>();

        // Services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IAuthAuditWriter, AuthAuditWriter>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<PermissionResolver>();

        // Menu tree cache (single-instance in-memory)
        services.AddMemoryCache();
        services.AddScoped<IMenuTreeCache, MenuTreeCache>();

        // Repositories
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();

        return services;
    }

    public static IApplicationBuilder UseAuthModule(this IApplicationBuilder app)
    {
        app.UseMigration<AuthDbContext>();

        // Apply the DB-maintained lockout settings to the IdentityOptions snapshot once, after the
        // policy row has been migrated + seeded. Done here (not via IConfigureOptions) because reading
        // the DB during options-configuration at container startup deadlocks. Lockout therefore takes
        // effect on the next restart — the admin screen states this.
        ApplyLockoutPolicy(app.ApplicationServices);

        return app;
    }

    private static void ApplyLockoutPolicy(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var policy = dbContext.PasswordPolicy.AsNoTracking().FirstOrDefault();
        if (policy is null) return;

        var lockout = scope.ServiceProvider
            .GetRequiredService<IOptions<IdentityOptions>>().Value.Lockout;
        lockout.AllowedForNewUsers = policy.LockoutEnabled;
        lockout.MaxFailedAccessAttempts = policy.MaxFailedAccessAttempts;
        lockout.DefaultLockoutTimeSpan = policy.LockoutMinutes <= 0
            ? TimeSpan.FromDays(365 * 200)
            : TimeSpan.FromMinutes(policy.LockoutMinutes);
    }

    private static AuthorizationBuilder AddPolicies(this AuthorizationBuilder authorizationBuilder, bool allowDevBypass)
    {
        authorizationBuilder
            .AddClientPermissionPolicy("CanReadAuth", ["auth:read"])
            .AddClientPermissionPolicy("CanWriteAuth", ["auth:read", "auth:write"])
            .AddClientPermissionPolicy("CanReadDocument", ["document:read"])
            .AddClientPermissionPolicy("CanWriteDocument", ["document:read", "document:write"])
            .AddClientPermissionPolicy("CanReadNotification", ["notification:read"])
            .AddClientPermissionPolicy(
                "CanWriteNotification",
                ["notification:read", "notification:write"]
            )
            .AddClientPermissionPolicy("CanReadRequest", ["request:read"])
            .AddClientPermissionPolicy("CanWriteRequest", ["request:read", "request:write"])
            .AddUserPermissionPolicy("CanManagePermissions", "PERMISSION_MANAGE")
            .AddUserPermissionPolicy("CanManageRoles", "ROLE_MANAGE")
            .AddUserPermissionPolicy("CanManageGroups", "GROUP_MANAGE")
            .AddUserPermissionPolicy("CanManageTeams", "TEAM_MANAGE")
            .AddUserPermissionPolicy("CanViewAuthAudit", "AUTH_AUDIT_VIEW")
            .AddUserPermissionPolicy("CanManageUsers", "USER_MANAGE")
            .AddUserPermissionPolicy("CanManageMenus", "MENU_MANAGE")
            .AddUserPermissionPolicy("CanManageCompanies", "COMPANY_MANAGE")
            .AddUserPermissionPolicy("CanChangeUserPassword", "USER_CHANGE_PASSWORD")
            .AddUserPermissionPolicy("CanResetUserPassword", "USER_RESET_PASSWORD")
            .AddUserPermissionPolicy("CanManagePasswordPolicy", "PASSWORD_POLICY_MANAGE")
            .AddUserPermissionPolicy("CanReleaseTaskLocks", "TASK_LOCK_MANAGE")
            // Meeting policies — TODO: replace with real role claims once role infrastructure is complete
            .AddUserPermissionPolicy("MeetingAdmin", "MEETING_ADMIN")
            .AddUserPermissionPolicy("MeetingSecretary", "MEETING_SECRETARY")
            .AddUserPermissionPolicy("CommitteeMember", "COMMITTEE_MEMBER")
            .AddScopePolicy("ClsReadAppraisal", "appraisal.read")
            .AddScopePolicy("ClsWriteRequest", "request.write")
            .AddScopePolicy("ClsReadDocument", "document.read")
            .AddScopePolicy("ClsWriteDocument", "document.write")
            .AddScopePolicy("Integration", "integration")
            .AddUserPermissionPolicy("workflow.admin", "WORKFLOW_ADMIN")
            .AddUserPermissionPolicy("WebhookDeliveriesView", "WEBHOOK_DELIVERIES_VIEW")
            .AddUserPermissionPolicy("WebhookDeliveriesRetry", "WEBHOOK_DELIVERIES_RETRY")
            .AddUserPermissionPolicy("LogsView", "LOGS_VIEW")
            .AddUserPermissionPolicy("task-monitor.view", "TASK_MONITOR_VIEW")
            .AddUserPermissionPolicy("task-monitor.reassign", "TASK_MONITOR_REASSIGN")
            .AddUserPermissionPolicy("history-search.view", "HISTORY_SEARCH_VIEW")
            .AddUserPermissionPolicy("reappraisal.generate-test-file", "REAPPRAISAL_GENERATE_TEST_FILE")
            // ── Monitoring feature policies (FSD §2.6.8) ──────────────────────────
            // Any-prefix policies: caller needs ANY permission with the given prefix.
            .AddMonitoringPrefixPolicy("monitoring.pending-internal", "MONITORING:PENDING_INTERNAL:")
            .AddMonitoringPrefixPolicy("monitoring.pending-external", "MONITORING:PENDING_EXTERNAL:")
            // Single-permission policies (admin screens — no layer split)
            .AddMonitoringAnyPolicy("monitoring.pending-quotation",
                ["MONITORING:PENDING_QUOTATION"])
            .AddMonitoringAnyPolicy("monitoring.pending-followup",
                ["MONITORING:PENDING_FOLLOWUP"])
            .AddMonitoringAnyPolicy("monitoring.pending-evaluation",
                ["MONITORING:PENDING_EVALUATION"])
            .AddMonitoringAnyPolicy("monitoring.meeting-followup",
                ["MONITORING:MEETING_FOLLOWUP"])
            // Top-breaches: visible to anyone with any OLA monitoring permission
            .AddMonitoringTopBreachesPolicy()
            // Hangfire dashboard: a server-rendered page opened by browser navigation, so it cannot carry a
            // JWT Bearer header. Gate on the interactive-login cookie (Identity.Application, set by
            // /Account/Login) and require the Admin role. Pinning the scheme is essential — without it the
            // policy would authenticate the default (Bearer) scheme and always fail for a browser navigation.
            .AddPolicy("HangfireDashboard", policy =>
                policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
                    .RequireAuthenticatedUser()
                    .RequireRole("Admin"));

        // When the dev-auth bypass is enabled (any non-production environment),
        // don't pin policies to the OpenIddict scheme so the PolicyScheme can
        // route to either DevBypass or OpenIddict.
        if (allowDevBypass)
        {
            authorizationBuilder
                .SetDefaultPolicy(
                    new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build()
                )
                .SetFallbackPolicy(
                    new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build()
                );
        }
        else
        {
            authorizationBuilder
                .SetDefaultPolicy(
                    new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .AddAuthenticationSchemes(
                            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
                        )
                        .Build()
                )
                .SetFallbackPolicy(
                    new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .AddAuthenticationSchemes(
                            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
                        )
                        .Build()
                );
        }

        return authorizationBuilder;
    }

    private static AuthorizationBuilder AddClientPermissionPolicy(
        this AuthorizationBuilder authorizationBuilder,
        string name,
        string[] allowedValues
    )
    {
        authorizationBuilder.AddPolicy(
            name,
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequirePermission(allowedValues);
            }
        );
        return authorizationBuilder;
    }

    private static void RequirePermission(
        this AuthorizationPolicyBuilder policy,
        params string[] allowedValues
    )
    {
        policy.RequireClaim("permissions", allowedValues);
    }

    private static AuthorizationBuilder AddUserPermissionPolicy(
        this AuthorizationBuilder authorizationBuilder,
        string policyName,
        string requiredPermission
    )
    {
        authorizationBuilder.AddPolicy(
            policyName,
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("permissions", requiredPermission);
            }
        );
        return authorizationBuilder;
    }

    private static AuthorizationBuilder AddScopePolicy(
        this AuthorizationBuilder authorizationBuilder,
        string policyName,
        string requiredScope
    )
    {
        authorizationBuilder.AddPolicy(
            policyName,
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.Claims
                        .Where(c => c.Type == "scope")
                        .SelectMany(c => c.Value.Split(' '))
                        .Contains(requiredScope));
            }
        );
        return authorizationBuilder;
    }

    /// <summary>
    /// Policy that passes when the user holds ANY "permissions" claim
    /// that starts with the given prefix (e.g. "MONITORING:PENDING_INTERNAL:").
    /// Used for layer-scoped monitoring screens where the specific layer suffix varies.
    /// </summary>
    private static AuthorizationBuilder AddMonitoringPrefixPolicy(
        this AuthorizationBuilder authorizationBuilder,
        string policyName,
        string permissionPrefix
    )
    {
        authorizationBuilder.AddPolicy(
            policyName,
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.Claims
                        .Where(c => c.Type == "permissions")
                        .Any(c => c.Value.StartsWith(permissionPrefix, StringComparison.OrdinalIgnoreCase)));
            }
        );
        return authorizationBuilder;
    }

    /// <summary>
    /// Policy for the top-breaches endpoint: passes when the user holds ANY OLA monitoring
    /// permission (Internal, External, or Followup).
    /// </summary>
    private static AuthorizationBuilder AddMonitoringTopBreachesPolicy(
        this AuthorizationBuilder authorizationBuilder)
    {
        authorizationBuilder.AddPolicy(
            "monitoring.top-breaches",
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.Claims
                        .Where(c => c.Type == "permissions")
                        .Any(c =>
                            c.Value.StartsWith("MONITORING:PENDING_INTERNAL:", StringComparison.OrdinalIgnoreCase) ||
                            c.Value.StartsWith("MONITORING:PENDING_EXTERNAL:", StringComparison.OrdinalIgnoreCase) ||
                            c.Value.Equals("MONITORING:PENDING_FOLLOWUP", StringComparison.OrdinalIgnoreCase)));
            }
        );
        return authorizationBuilder;
    }

    /// <summary>
    /// Policy that passes when the user holds ANY of the listed exact permission codes.
    /// Used for admin-level monitoring screens with a flat permission model.
    /// </summary>
    private static AuthorizationBuilder AddMonitoringAnyPolicy(
        this AuthorizationBuilder authorizationBuilder,
        string policyName,
        string[] permissionCodes
    )
    {
        authorizationBuilder.AddPolicy(
            policyName,
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.Claims
                        .Where(c => c.Type == "permissions")
                        .Any(c => permissionCodes.Contains(c.Value, StringComparer.OrdinalIgnoreCase)));
            }
        );
        return authorizationBuilder;
    }
}