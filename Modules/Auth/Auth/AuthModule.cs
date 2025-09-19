using Auth.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Auth.Permissions;
using OpenIddict.Validation.AspNetCore;

namespace Auth;

public static class AuthModule
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });

        services.AddAuthorizationBuilder()
            .AddPolicy("CanReadRequest", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequirePermission("request:read");
            })
            .AddPolicy("CanWriteRequest", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequirePermission("request:read", "request:write");
            })
            .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, "Identity.Application") // JwtBearerDefaults.AuthenticationScheme
                .Build());

        services.AddScoped<IRegistrationService, RegistrationService>();

        services.AddSingleton<IAuthorizationHandler, PermissionsHandler>();

        return services;
    }

    public static IApplicationBuilder UseAuthModule(this IApplicationBuilder app)
    {
        return app;
    }

    private static void RequirePermission(this AuthorizationPolicyBuilder policy, params string[] allowedValues) {
        policy.RequireClaim("permissions", allowedValues);
    }
}