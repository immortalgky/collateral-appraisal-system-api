using Auth.Application.Configurations;
using Auth.Domain.Companies;
using Auth.Infrastructure.Repository;
using Auth.Infrastructure.Seed;
using Auth.Application.Services;
using Auth.Contracts.Users;
using Auth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        var lockoutConfig = configuration.GetSection("Identity:Lockout");
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 8;

                options.Lockout.MaxFailedAccessAttempts = lockoutConfig.GetValue("MaxFailedAccessAttempts", 5);
                options.Lockout.DefaultLockoutTimeSpan = lockoutConfig.GetValue("PermanentLockout", true)
                    ? TimeSpan.FromDays(365 * 200)
                    : TimeSpan.FromMinutes(lockoutConfig.GetValue("DefaultLockoutTimeSpanInMinutes", 5));
                options.Lockout.AllowedForNewUsers = lockoutConfig.GetValue("LockoutEnabled", true);
            })
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddDefaultTokenProviders();

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
        var isDevelopment = environment == "Development";
        if (isDevelopment)
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
        services.AddAuthorizationBuilder().AddPolicies(isDevelopment);

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

        return services;
    }

    public static IApplicationBuilder UseAuthModule(this IApplicationBuilder app)
    {
        app.UseMigration<AuthDbContext>();

        return app;
    }

    private static AuthorizationBuilder AddPolicies(this AuthorizationBuilder authorizationBuilder, bool isDevelopment)
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
            .AddUserPermissionPolicy("CanManageUsers", "USER_MANAGE")
            .AddUserPermissionPolicy("CanManageMenus", "MENU_MANAGE")
            .AddUserPermissionPolicy("CanManageCompanies", "COMPANY_MANAGE")
            .AddUserPermissionPolicy("CanChangeUserPassword", "USER_CHANGE_PASSWORD")
            .AddUserPermissionPolicy("CanResetUserPassword", "USER_RESET_PASSWORD")
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
            .AddMonitoringTopBreachesPolicy();

        // In Development, don't pin policies to OpenIddict scheme so the
        // PolicyScheme can route to either DevBypass or OpenIddict.
        if (isDevelopment)
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