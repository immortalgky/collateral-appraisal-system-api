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
                    ? TimeSpan.MaxValue
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

                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(7));
                options.SetIdentityTokenLifetime(TimeSpan.FromMinutes(15));

                if (environment == "Development") options.AcceptAnonymousClients();

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.OfflineAccess,
                    "appraisal.read", "request.write", "document.read", "document.write");

                if (environment == "Development")
                {
                    options.AddDevelopmentEncryptionCertificate();
                    options.AddDevelopmentSigningCertificate();
                }
                else
                {
                    var signingCertConfig = configuration.GetSection("OAuth2:SigningCertificate");
                    var encryptionCertConfig = configuration.GetSection("OAuth2:EncryptionCertificate");

                    if (signingCertConfig.Exists())
                        throw new InvalidOperationException(
                            "Production signing certificate configuration required but not implemented. Please configure OAuth2:SigningCertificate section.");

                    if (encryptionCertConfig.Exists())
                        throw new InvalidOperationException(
                            "Production encryption certificate configuration required but not implemented. Please configure OAuth2:EncryptionCertificate section.");

                    options.AddDevelopmentEncryptionCertificate();
                    options.AddDevelopmentSigningCertificate();
                }

                if (environment == "Development") options.DisableAccessTokenEncryption();

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
            .AddUserPermissionPolicy("CanChangeUserPassword", "USER_CHANGE_PASSWORD")
            .AddUserPermissionPolicy("CanResetUserPassword", "USER_RESET_PASSWORD")
            .AddScopePolicy("ClsReadAppraisal", "appraisal.read")
            .AddScopePolicy("ClsWriteRequest", "request.write")
            .AddScopePolicy("ClsReadDocument", "document.read")
            .AddScopePolicy("ClsWriteDocument", "document.write");

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
                policy.RequireClaim("scope", requiredScope);
            }
        );
        return authorizationBuilder;
    }
}