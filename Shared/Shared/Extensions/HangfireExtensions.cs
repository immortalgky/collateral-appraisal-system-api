using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;

namespace Shared.Extensions;

public static class HangfireExtensions
{
    public static IServiceCollection AddHangfire(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHangfire(config =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(configuration.GetConnectionString("Hangfire"),
                    new SqlServerStorageOptions
                    {
                        SchemaName = "hangfire",
                        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                        QueuePollInterval = TimeSpan.Zero,
                        UseRecommendedIsolationLevel = true,
                        DisableGlobalLocks = true
                    }
                );
        });

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 5;
            options.ServerName = "CollateralAppraisalSystem-Worker";
        });

        return services;
    }

    public static WebApplication UseHangfire(this WebApplication app)
    {
        // The dashboard is a server-rendered page opened by a top-level browser navigation, which cannot
        // carry a JWT Bearer header — so the SPA's bearer auth can't gate it. Instead:
        //   - Dev: .AllowAnonymous() opts out of the global RequireAuthenticatedUser fallback policy (same
        //     pattern as /openapi, /scalar); the HangfireAuthorizationFilter allows localhost.
        //   - Non-dev: .RequireAuthorization("HangfireDashboard") makes the authorization middleware
        //     authenticate the Identity.Application cookie (set by the interactive /Account/Login) and
        //     require the Admin role; an unauthenticated browser is redirected to the login page.
        var isDevelopment = app.Environment.IsDevelopment();
        var dashboard = app.MapHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireAuthorizationFilter(isDevelopment) },
            DashboardTitle = "Hangfire Dashboard"
        });

        if (isDevelopment)
            dashboard.AllowAnonymous();
        else
            dashboard.RequireAuthorization("HangfireDashboard");

        return app;
    }
}

public class HangfireAuthorizationFilter(bool isDevelopment) :
    IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Development: allow without authentication. Gated purely on the environment (not the request
        // hostname) so the dashboard is reachable in dev regardless of how the app is addressed
        // (localhost, container name, LAN IP). The endpoint uses .AllowAnonymous() in dev, so this
        // filter is the only gate; in non-dev the "HangfireDashboard" policy gates instead.
        if (isDevelopment)
            return true;

        // Non-development: defense-in-depth. The "HangfireDashboard" endpoint policy
        // (RequireAuthorization in UseHangfire) has already authenticated the Identity.Application
        // cookie and enforced the Admin role before this filter runs, so HttpContext.User is the
        // authenticated cookie principal here.
        return httpContext.User.Identity?.IsAuthenticated == true;
    }
}