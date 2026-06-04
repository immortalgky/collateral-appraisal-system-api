using Common.Application.Features.Monitoring.Shared;
using Common.Infrastructure;
using Common.Infrastructure.Configuration;
using Common.Infrastructure.Seed;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;
using Shared.Data.Extensions;
using Shared.Data.Seed;

namespace Common;

public static class CommonModule
{
    public static IServiceCollection AddCommonModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CommonDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure();
                sqlOptions.MigrationsAssembly(typeof(CommonDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "common");
            });
        });

        // Monitoring feature services
        services.AddScoped<MonitoringScopeService>();

        // System configuration reader (cross-module, backed by IMemoryCache)
        services.AddMemoryCache();
        services.AddScoped<ISystemConfigurationReader, SystemConfigurationReader>();

        // Seeders
        services.AddScoped<IDataSeeder<CommonDbContext>, SystemConfigurationDataSeed>();

        return services;
    }

    public static IApplicationBuilder UseCommonModule(this IApplicationBuilder app)
    {
        app.UseMigration<CommonDbContext>();

        return app;
    }
}
