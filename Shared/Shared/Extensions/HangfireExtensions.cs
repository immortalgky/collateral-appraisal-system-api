using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Shared.Configurations;

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

    /// <summary>
    /// Lets the SPA embed the Hangfire dashboard in an iframe (its /admin/hangfire page).
    /// MUST be called before UseRouting — see the comment inside.
    /// </summary>
    /// <remarks>
    /// Call site is load-bearing. Registered where UseHangfire() sits (after UseRouting and the Map*
    /// calls) this middleware never executes: probe headers written both synchronously and from
    /// OnStarting were absent from /hangfire and /metrics responses, on a freshly built and freshly
    /// started process, twice. Moved above UseRouting it works. If you need to reason about this
    /// again, re-run that probe rather than trusting pipeline theory — and check the PID serving the
    /// port really is the process you just built, since a leftover instance will happily answer.
    /// </remarks>
    public static WebApplication UseHangfireDashboardFraming(this WebApplication app)
    {
        // ASP.NET Core's antiforgery stamps "X-Frame-Options: SAMEORIGIN" on every dashboard response
        // (Hangfire asks it for a token), and the SPA is a different origin — same host, different
        // port — so that header blocks the iframe. Replace it, for /hangfire only, with a
        // frame-ancestors directive naming the SPA origins already trusted for CORS: our own SPA may
        // embed the dashboard, nobody else may. Every other page — the login page above all — keeps
        // X-Frame-Options untouched. With no configured origin the directive collapses to 'self', so a
        // misconfigured environment fails closed.
        //
        // Registration order matters twice over:
        //   - Before UseRouting: middleware registered after it (where UseHangfire sits) never runs for
        //     a request the endpoint middleware handles, so the header swap would silently do nothing.
        //   - Via OnStarting: antiforgery writes the header while the endpoint runs, long after this
        //     middleware has handed off. OnStarting callbacks fire last-registered-first, so registering
        //     from the earliest middleware means this one runs last and wins.
        var allowedOrigins = app.Configuration
            .GetSection(CorsConfiguration.SectionName)
            .Get<CorsConfiguration>()?.AllowedOrigins ?? [];
        var frameAncestors = string.Join(' ', new[] { "'self'" }.Concat(allowedOrigins));

        app.UseWhen(
            context => context.Request.Path.StartsWithSegments("/hangfire"),
            branch => branch.Use(async (context, next) =>
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.Remove("X-Frame-Options");
                    context.Response.Headers["Content-Security-Policy"] = $"frame-ancestors {frameAncestors}";
                    return Task.CompletedTask;
                });

                await next();
            }));

        return app;
    }

    public static WebApplication UseHangfire(this WebApplication app)
    {
        // The dashboard is a server-rendered page loaded by the browser itself, which cannot carry a
        // JWT Bearer header — so the SPA's bearer auth can't gate it. Instead:
        //   - Dev: .AllowAnonymous() opts out of the global RequireAuthenticatedUser fallback policy (same
        //     pattern as /openapi, /scalar), and HangfireAuthorizationFilter lets every request through
        //     — including one arriving over the LAN, since it gates on the environment and not on the
        //     request's host. A dev instance bound to anything but localhost therefore serves an
        //     unauthenticated, fully interactive job dashboard.
        //   - Non-dev: .RequireAuthorization("HangfireDashboard") makes the authorization middleware
        //     authenticate the Identity.Application cookie (set by the interactive /Account/Login) and
        //     require the JOB_SCHEDULE_MANAGE permission, read from the database because the cookie
        //     carries no permission claims (see DatabasePermissionAuthorizationHandler). An
        //     unauthenticated browser is redirected to the login page; a signed-in user without the
        //     permission lands on /Account/AccessDenied.
        var isDevelopment = app.Environment.IsDevelopment();
        var dashboard = app.MapHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireAuthorizationFilter(isDevelopment) },
            DashboardTitle = "Hangfire Dashboard",
            // Hide Hangfire's "Back to site" link: inside the SPA's iframe it would navigate the frame
            // to the API root, which is not a page. The SPA's own navigation is the way out.
            AppPath = null
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
        // cookie and enforced the JOB_SCHEDULE_MANAGE permission before this filter runs, so
        // HttpContext.User is the authenticated cookie principal here.
        return httpContext.User.Identity?.IsAuthenticated == true;
    }
}