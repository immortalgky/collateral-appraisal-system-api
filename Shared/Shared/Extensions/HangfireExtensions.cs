using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    public static IApplicationBuilder UseHangfire(this IApplicationBuilder app)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireAuthorizationFilter() },
            DashboardTitle = "Hangfire Dashboard"
        });

        return app;
    }
}

public class HangfireAuthorizationFilter :
    IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // TODO: Implement proper authorization
        // For now, allow only in Development
        var httpContext = context.GetHttpContext();

        // Development: Allow all
        if (httpContext.Request.Host.Host.Contains("localhost"))
            return true;

        // Production: Add your auth logic here
        // Example: Check if user is authenticated and has admin role
        // return httpContext.User.Identity?.IsAuthenticated == true 
        //     && httpContext.User.IsInRole("Admin");

        return false; // Block by default in production
    }
}