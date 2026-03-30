using Common.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data.Extensions;

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

        return services;
    }

    public static IApplicationBuilder UseCommonModule(this IApplicationBuilder app)
    {
        app.UseMigration<CommonDbContext>();

        return app;
    }
}
