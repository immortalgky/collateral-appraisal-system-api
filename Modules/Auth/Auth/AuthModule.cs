using Auth.Permissions;
using Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Validation.AspNetCore;

namespace Auth;

public static class AuthModule
{
    public static IServiceCollection AddAuthModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme =
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme =
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });

        services.AddAuthorizationBuilder().AddPolicies();

        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IPermissionService, PermissionService>();

        services.AddSingleton<IAuthorizationHandler, PermissionsHandler>();

        return services;
    }

    public static IApplicationBuilder UseAuthModule(this IApplicationBuilder app)
    {
        return app;
    }

    private static AuthorizationBuilder AddPolicies(this AuthorizationBuilder authorizationBuilder)
    {
        authorizationBuilder
            .AddClientPermissionPolicy("CanReadAuth", ["auth:read"])
            .AddClientPermissionPolicy("CanWriteAuth", ["auth:read", "auth:write"])
            .AddClientPermissionPolicy("CanReadDocument", ["document:read"])
            .AddClientPermissionPolicy("CanWriteDocument", ["document:read", "document:write"])
            .AddClientPermissionPolicy("CanReadDocument", ["notification:read"])
            .AddClientPermissionPolicy(
                "CanWriteDocument",
                ["notification:read", "notification:write"]
            )
            .AddClientPermissionPolicy("CanReadRequest", ["request:read"])
            .AddClientPermissionPolicy("CanWriteRequest", ["request:read", "request:write"])
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
}
